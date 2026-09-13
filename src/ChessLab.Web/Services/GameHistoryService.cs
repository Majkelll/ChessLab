using ChessLab.Core.Contracts;
using ChessLab.Data;
using Microsoft.EntityFrameworkCore;

namespace ChessLab.Web.Services;

public sealed class GameHistoryService(ChessLabDbContext db)
{
    public async Task<IReadOnlyList<GameHistoryEntryDto>> ForUserAsync(Guid userId, int limit)
    {
        var records = await db.GameRecords
            .AsNoTracking()
            .Include(g => g.Players)
            .Where(g => g.Players.Any(p => p.UserId == userId))
            .OrderByDescending(g => g.FinishedAtUtc)
            .Take(limit)
            .ToListAsync();

        return records.Select(ToEntry).ToArray();
    }

    public Task<GameHistoryDetailDto?> ByIdAsync(Guid id) =>
        DetailAsync(g => g.Id == id);

    public Task<GameHistoryDetailDto?> ByRoomCodeAsync(string roomCode) =>
        DetailAsync(g => g.RoomCode == roomCode);

    private async Task<GameHistoryDetailDto?> DetailAsync(
        System.Linq.Expressions.Expression<Func<GameRecord, bool>> predicate)
    {
        var record = await db.GameRecords
            .AsNoTracking()
            .Include(g => g.Players)
            .FirstOrDefaultAsync(predicate);

        if (record is null)
            return null;

        var moves = record.MovesSan.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return new GameHistoryDetailDto(ToEntry(record), record.FinalFen, moves);
    }

    private static GameHistoryEntryDto ToEntry(GameRecord record) => new(
        record.Id,
        record.RoomCode,
        record.Kind,
        DateTime.SpecifyKind(record.FinishedAtUtc, DateTimeKind.Utc),
        record.EndReason,
        record.Winner,
        record.MoveCount,
        record.Players
            .OrderBy(p => p.Side)
            .ThenBy(p => p.Role)
            .Select(p => new GameHistoryPlayerDto(p.Side, p.Role, p.UserId, p.DisplayName, p.BotDifficulty))
            .ToArray());
}
