using System.Collections.Concurrent;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Web.Services;

public sealed class BotRunner(
    RoomRegistry registry,
    GameBroadcaster broadcaster,
    IServiceScopeFactory scopeFactory,
    BotAvailability availability,
    TimeProvider time,
    ILogger<BotRunner> logger)
{
    public const string PausedNotice = "The bot is unavailable. The game is paused until it is back.";
    public const string ResumedNotice = "The bot is back. The game goes on.";

    private const int MaxRoundsWithoutProgress = 5;

    private static readonly TimeSpan MinThinkDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan MaxThinkDelay = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<string, SemaphoreSlim> roomLocks = new();
    private readonly ConcurrentDictionary<string, byte> retriesPending = new();

    public void ScheduleBotTurns(string code) => _ = RunPendingBotTurnsAsync(code);

    public void Forget(string code)
    {
        roomLocks.TryRemove(code, out _);
        retriesPending.TryRemove(code, out _);
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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The bot could not act in room {Code}.", code);
            await PauseAsync(session);
        }
        finally
        {
            roomLock.Release();
        }
    }

    private async Task PlayRoundsAsync(IRoomSession session)
    {
        var roundsWithoutProgress = 0;

        while (BotSeatsToMove(session) is { Length: > 0 } botSeats)
        {
            await Task.Delay(Random.Shared.Next((int)MinThinkDelay.TotalMilliseconds, (int)MaxThinkDelay.TotalMilliseconds));

            var progressed = false;
            var resumed = false;
            foreach (var seat in botSeats)
            {
                var outcome = await PlaySeatAsync(session, seat);
                progressed |= outcome != SeatOutcome.Rejected;
                resumed |= outcome == SeatOutcome.Resumed;
            }

            if (resumed)
                await broadcaster.NoticeAsync(session.Room.Code, ResumedNotice);

            roundsWithoutProgress = progressed ? 0 : roundsWithoutProgress + 1;
            if (roundsWithoutProgress >= MaxRoundsWithoutProgress)
                throw new InvalidOperationException("The bot keeps choosing actions the rules reject.");

            await broadcaster.GameChangedAsync(session, "GameUpdated");
        }
    }

    private static SeatId[] BotSeatsToMove(IRoomSession session)
    {
        lock (session)
            return session.HasActiveGame ? BotSeatsAmong(session, session.ActiveSeats) : [];
    }

    private static SeatId[] BotSeatsAmong(IRoomSession session, IEnumerable<SeatId> seats) =>
        [.. seats.Where(seat => session.Room.Seats[seat].Kind == OccupantKind.Bot)];

    private async Task<SeatOutcome> PlaySeatAsync(IRoomSession session, SeatId seat)
    {
        BotActionRequest request;
        DateTimeOffset askedAt;
        lock (session)
        {
            if (!session.HasActiveGame || !session.ActiveSeats.Contains(seat))
                return SeatOutcome.Skipped;

            request = new BotActionRequest(
                session.Room.Kind, seat, session.Room.Seats[seat].Difficulty!.Value, session.ToStateDto(seat.Side));
            askedAt = time.GetUtcNow();
        }

        var action = await AskBotServiceAsync(request);

        lock (session)
        {
            if (!session.HasActiveGame || !session.ActiveSeats.Contains(seat))
                return SeatOutcome.Skipped;

            var wasPaused = session.IsPaused;
            try
            {
                session.ApplyBotAction(action, seat, askedAt, time.GetUtcNow());
                return wasPaused ? SeatOutcome.Resumed : SeatOutcome.Applied;
            }
            catch (InvalidOperationException ex)
            {
                logger.LogDebug(ex, "Bot action {Action} rejected in room {Code}.", action.Kind, session.Room.Code);
                return SeatOutcome.Rejected;
            }
        }
    }

    private async Task<GameAction> AskBotServiceAsync(BotActionRequest request)
    {
        using var limit = new CancellationTokenSource(BotServiceSettings.RequestLimit);
        using var scope = scopeFactory.CreateScope();
        try
        {
            var action = await scope.ServiceProvider.GetRequiredService<BotServiceClient>().ChooseActionAsync(request, limit.Token);
            availability.Report(succeeded: true);
            return action;
        }
        catch
        {
            availability.Report(succeeded: false);
            throw;
        }
    }

    private async Task PauseAsync(IRoomSession session)
    {
        bool pausedNow;
        bool stillWaiting;
        lock (session)
        {
            var botMustAct = session.HasActiveGame && BotSeatsAmong(session, session.ActiveSeats).Length > 0;
            pausedNow = botMustAct && !session.IsPaused;
            if (pausedNow)
                session.Pause(time.GetUtcNow());

            stillWaiting = botMustAct;
        }

        if (pausedNow)
        {
            await broadcaster.NoticeAsync(session.Room.Code, PausedNotice);
            await broadcaster.GameChangedAsync(session, "GameUpdated");
        }

        if (stillWaiting)
            RetryLater(session.Room.Code);
    }

    private void RetryLater(string code)
    {
        if (!retriesPending.TryAdd(code, 0))
            return;

        _ = Task.Delay(RetryDelay).ContinueWith(delay =>
        {
            retriesPending.TryRemove(code, out _);
            ScheduleBotTurns(code);
        }, TaskScheduler.Default);
    }

    private enum SeatOutcome
    {
        Skipped,
        Applied,
        Resumed,
        Rejected,
    }
}
