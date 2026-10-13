using System.Collections.Concurrent;
using ChessLab.Bots;
using ChessLab.Core.Rooms;

namespace ChessLab.Web.Services;

public sealed class BotRunner(
    RoomRegistry registry,
    GameBroadcaster broadcaster,
    IConfiguration configuration,
    ILogger<BotRunner> logger) : IAsyncDisposable
{
    private const int MaxConsecutiveFailures = 3;
    private const int MaxRoundsWithoutProgress = 5;
    private const string BotGaveUpNotice =
        "A bot stopped responding. The game goes on when a player acts or the clock runs out.";

    private static readonly TimeSpan MinThinkDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan MaxThinkDelay = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan ThinkingLimit = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<string, StockfishEngine> engines = new();
    private readonly ConcurrentDictionary<string, IGameBot> bots = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> roomLocks = new();
    private readonly ConcurrentDictionary<string, int> consecutiveFailures = new();

    public void ScheduleBotTurns(string code) => _ = RunPendingBotTurnsAsync(code);

    public async Task ForgetAsync(string code)
    {
        consecutiveFailures.TryRemove(code, out _);
        roomLocks.TryRemove(code, out _);
        await ReleaseBotAsync(code);
    }

    private async Task RunPendingBotTurnsAsync(string code)
    {
        if (registry.Find(code) is not { } session)
            return;

        var roomLock = roomLocks.GetOrAdd(code, static _ => new SemaphoreSlim(1, 1));
        await roomLock.WaitAsync();
        try
        {
            await PlayRoundsAsync(session);
            consecutiveFailures.TryRemove(code, out _);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bot turn processing failed for room {Code}.", code);
            await ReleaseBotAsync(code);
            await RetryLaterAsync(code);
        }
        finally
        {
            if (!session.HasActiveGame)
                await ReleaseBotAsync(code);

            roomLock.Release();
        }
    }

    private async Task PlayRoundsAsync(IRoomSession session)
    {
        var code = session.Room.Code;
        var roundsWithoutProgress = 0;

        while (BotSeatsToMove(session) is { Length: > 0 } botSeats)
        {
            await Task.Delay(Random.Shared.Next((int)MinThinkDelay.TotalMilliseconds, (int)MaxThinkDelay.TotalMilliseconds));

            var bot = await GetOrCreateBotAsync(code, session.Room.Kind);

            var progressed = false;
            foreach (var seat in botSeats)
                progressed |= await PlaySeatAsync(session, bot, seat);

            roundsWithoutProgress = progressed ? 0 : roundsWithoutProgress + 1;
            if (roundsWithoutProgress >= MaxRoundsWithoutProgress)
                throw new InvalidOperationException("The bot keeps choosing actions the rules reject.");

            await broadcaster.GameChangedAsync(session, "GameUpdated");
        }
    }

    private async Task RetryLaterAsync(string code)
    {
        var failures = consecutiveFailures.AddOrUpdate(code, 1, static (_, count) => count + 1);
        if (failures >= MaxConsecutiveFailures)
        {
            logger.LogError("Giving up on bot turns in room {Code} after {Failures} failures in a row.", code, failures);
            await broadcaster.NoticeAsync(code, BotGaveUpNotice);
            return;
        }

        _ = Task.Delay(RetryDelay).ContinueWith(_ => ScheduleBotTurns(code), TaskScheduler.Default);
    }

    private static SeatId[] BotSeatsToMove(IRoomSession session)
    {
        lock (session)
        {
            if (!session.HasActiveGame)
                return [];

            return [.. session.ActiveSeats.Where(seat => session.Room.Seats[seat].Kind == OccupantKind.Bot)];
        }
    }

    private async Task<bool> PlaySeatAsync(IRoomSession session, IGameBot bot, SeatId seat)
    {
        var difficulty = session.Room.Seats[seat].Difficulty!.Value;

        using var thinkingLimit = new CancellationTokenSource(ThinkingLimit);
        var action = await bot.ChooseActionAsync(session, seat, difficulty, thinkingLimit.Token);

        lock (session)
        {
            if (!session.HasActiveGame || !session.ActiveSeats.Contains(seat))
                return true;

            try
            {
                session.Apply(action, seat, DateTimeOffset.UtcNow);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogDebug(ex, "Bot action {Action} rejected in room {Code}.", action.Kind, session.Room.Code);
                return false;
            }
        }
    }

    private async Task<IGameBot> GetOrCreateBotAsync(string code, GameKind kind)
    {
        if (bots.TryGetValue(code, out var existing))
            return existing;

        var engine = GameBots.NeedsChessEngine(kind) ? await GetOrCreateEngineAsync(code) : null;
        var bot = GameBots.For(kind, engine);
        bots[code] = bot;
        return bot;
    }

    private async Task<StockfishEngine> GetOrCreateEngineAsync(string code)
    {
        if (engines.TryGetValue(code, out var existing))
            return existing;

        var path = configuration["Bots:StockfishPath"] ?? "stockfish";
        using var startLimit = new CancellationTokenSource(ThinkingLimit);
        var engine = await StockfishEngine.StartAsync(path, startLimit.Token);
        engines[code] = engine;
        return engine;
    }

    private async Task ReleaseBotAsync(string code)
    {
        bots.TryRemove(code, out _);
        if (engines.TryRemove(code, out var engine))
            await engine.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var engine in engines.Values)
            await engine.DisposeAsync();
    }
}
