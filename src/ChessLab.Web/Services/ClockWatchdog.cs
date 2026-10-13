using ChessLab.Core.Rooms;

namespace ChessLab.Web.Services;

public sealed class ClockWatchdog(
    RoomRegistry registry,
    GameBroadcaster broadcaster,
    BotRunner botRunner,
    RoomConnections connections,
    GameArchive archive,
    ILogger<ClockWatchdog> logger) : BackgroundService
{
    private const int TicksBetweenSweeps = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        var ticksSinceSweep = 0;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var session in registry.ActiveSessions())
                await EndGameIfTimeRanOutAsync(session, stoppingToken);

            if (++ticksSinceSweep < TicksBetweenSweeps)
                continue;

            ticksSinceSweep = 0;
            await RemoveStaleRoomsAsync();
        }
    }

    private async Task EndGameIfTimeRanOutAsync(IRoomSession session, CancellationToken stoppingToken)
    {
        try
        {
            if (!TimeRanOut(session))
                return;

            await broadcaster.GameChangedAsync(session, "GameUpdated", stoppingToken);
            botRunner.ScheduleBotTurns(session.Room.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Clock check failed for room {RoomCode}.", session.Room.Code);
        }
    }

    private static bool TimeRanOut(IRoomSession session)
    {
        lock (session)
        {
            if (!session.HasActiveGame)
                return false;

            session.DeclareTimeoutIfExpired(DateTimeOffset.UtcNow);
            return !session.HasActiveGame;
        }
    }

    private async Task ArchiveFinishedGamesAsync()
    {
        foreach (var session in registry.FinishedSessions())
        {
            if (!archive.IsArchived(session.Room.Code))
                await archive.RecordIfFinishedAsync(session);
        }
    }

    private async Task RemoveStaleRoomsAsync()
    {
        await ArchiveFinishedGamesAsync();

        foreach (var code in registry.RemoveStale(DateTimeOffset.UtcNow, archive.IsArchived))
        {
            try
            {
                connections.Forget(code);
                archive.Forget(code);
                await botRunner.ForgetAsync(code);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Cleaning up after room {RoomCode} failed.", code);
            }
        }
    }
}
