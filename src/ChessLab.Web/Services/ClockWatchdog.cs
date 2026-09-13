using ChessLab.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Services;

/// <summary>Ends games by timeout once a side's clock reaches zero, even if nobody calls the hub in the meantime.</summary>
public sealed class ClockWatchdog(RoomRegistry registry, IHubContext<GameHub> hub, GameArchive archive) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            foreach (var session in registry.ActiveSessions())
            {
                session.DeclareTimeoutIfExpired(DateTimeOffset.UtcNow);
                if (!session.Game!.IsGameOver)
                    continue;

                await hub.Clients.Group(session.Room.Code)
                    .SendAsync("GameUpdated", GameDtoMapper.ToGameUpdateDto(session), stoppingToken);
                await archive.RecordIfFinishedAsync(session);
            }

            foreach (var session in registry.ActiveCardChessSessions())
            {
                session.DeclareTimeoutIfExpired(DateTimeOffset.UtcNow);
                if (!session.Game!.IsGameOver)
                    continue;

                await hub.Clients.Group(session.Room.Code)
                    .SendAsync("CardChessGameUpdated", GameDtoMapper.ToCardChessUpdateDto(session), stoppingToken);
                await archive.RecordIfFinishedAsync(session);
            }

            foreach (var session in registry.ActiveArcaneChessSessions())
            {
                session.DeclareTimeoutIfExpired(DateTimeOffset.UtcNow);
                if (!session.Game!.IsGameOver)
                    continue;

                await hub.Clients.Group(session.Room.Code)
                    .SendAsync("ArcaneChessGameUpdated", GameDtoMapper.ToArcaneChessUpdateDto(session), stoppingToken);
                await archive.RecordIfFinishedAsync(session);
            }
        }
    }
}
