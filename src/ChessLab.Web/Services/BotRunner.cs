using System.Collections.Concurrent;
using ChessLab.Bots;
using ChessLab.Core.Rooms;
using ChessLab.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Services;

public sealed class BotRunner(RoomRegistry registry, IHubContext<GameHub> hub, GameArchive archive, IConfiguration configuration, ILogger<BotRunner> logger)
    : IAsyncDisposable
{
    private static readonly TimeSpan MinThinkDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan MaxThinkDelay = TimeSpan.FromMilliseconds(900);

    private readonly ConcurrentDictionary<string, StockfishEngine> engines = new();
    private readonly ConcurrentDictionary<string, IGameBot> bots = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> roomLocks = new();

    public void ScheduleBotTurns(string code) => _ = RunPendingBotTurnsAsync(code);

    private async Task RunPendingBotTurnsAsync(string code)
    {
        var roomLock = roomLocks.GetOrAdd(code, static _ => new SemaphoreSlim(1, 1));
        await roomLock.WaitAsync();
        try
        {
            IRoomSession session;
            try
            {
                session = registry.Get(code);
            }
            catch (HubException)
            {
                return;
            }

            while (session.HasActiveGame)
            {
                var botSeats = session.ActiveSeats
                    .Where(seat => session.Room.Seats[seat].Kind == OccupantKind.Bot)
                    .ToArray();

                if (botSeats.Length == 0)
                    return;

                await Task.Delay(Random.Shared.Next((int)MinThinkDelay.TotalMilliseconds, (int)MaxThinkDelay.TotalMilliseconds));

                var bot = await GetOrCreateBotAsync(code, session.Room.Kind);

                foreach (var seat in botSeats)
                {
                    var difficulty = session.Room.Seats[seat].Difficulty!.Value;
                    var action = await bot.ChooseActionAsync(session, seat, difficulty);

                    try
                    {
                        session.Apply(action, seat, DateTimeOffset.UtcNow);
                    }
                    catch (InvalidOperationException ex)
                    {
                        logger.LogDebug(ex, "Bot action {Action} rejected in room {Code}.", action.Kind, code);
                    }
                }

                await hub.Clients.Group(code).SendAsync("GameUpdated", session.ToUpdateDto());
                await archive.RecordIfFinishedAsync(session);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bot turn processing failed for room {Code}.", code);
        }
        finally
        {
            roomLock.Release();
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
        var engine = await StockfishEngine.StartAsync(path);
        engines[code] = engine;
        return engine;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var engine in engines.Values)
            await engine.DisposeAsync();
    }
}
