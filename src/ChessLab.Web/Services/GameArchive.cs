using System.Collections.Concurrent;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using ChessLab.Data;
using Microsoft.EntityFrameworkCore;

namespace ChessLab.Web.Services;

/// <summary>
/// Writes a finished game to the database so it outlives the room, which only exists in memory and
/// disappears with the next restart. Called from every path that can end a game (a hub action, a
/// bot's move, the clock watchdog), so it has to tolerate being called repeatedly for the same
/// room and for games that are still in progress.
/// </summary>
public sealed class GameArchive(IServiceScopeFactory scopeFactory, ILogger<GameArchive> logger)
{
    private readonly ConcurrentDictionary<string, byte> archived = new();

    public async Task RecordIfFinishedAsync(IRoomSession session)
    {
        if (Snapshot(session) is not { } snapshot)
            return;

        if (!archived.TryAdd(session.Room.Code, 0))
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ChessLabDbContext>();

            if (await db.GameRecords.AnyAsync(g => g.RoomCode == snapshot.RoomCode))
                return;

            db.GameRecords.Add(snapshot);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            archived.TryRemove(session.Room.Code, out _);
            logger.LogError(ex, "Failed to archive finished game in room {RoomCode}.", session.Room.Code);
        }
    }

    private static GameRecord? Snapshot(IRoomSession session) => session switch
    {
        GameSession { Game: { IsGameOver: true } game } =>
            Build(session.Room, game.EndResult!.Value, game.MoveHistory, game.ToFen()),
        CardChessSession { Game: { IsGameOver: true } game } =>
            Build(session.Room, game.EndResult!.Value, game.MoveHistory, game.ToFen()),
        ArcaneChessSession { Game: { IsGameOver: true } game } =>
            Build(session.Room, game.EndResult!.Value, game.MoveHistory, game.ToFen()),
        _ => null,
    };

    private static GameRecord Build(Room room, GameEndResult result, IReadOnlyList<ChessMove> moves, string finalFen) => new()
    {
        Id = Guid.NewGuid(),
        RoomCode = room.Code,
        Kind = room.Kind,
        FinishedAtUtc = DateTime.UtcNow,
        EndReason = result.Reason,
        Winner = result.Winner,
        MoveCount = moves.Count,
        MovesSan = string.Join(' ', moves.Select(m => m.San)),
        FinalFen = finalFen,
        Players = room.Seats
            .Where(seat => seat.Value.Kind != OccupantKind.Empty)
            .Select(seat => new GameRecordPlayer
            {
                Id = Guid.NewGuid(),
                Side = seat.Key.Side,
                Role = seat.Key.Role,
                UserId = seat.Value.UserId,
                DisplayName = seat.Value.DisplayName,
                BotDifficulty = seat.Value.Difficulty,
            })
            .ToList(),
    };
}
