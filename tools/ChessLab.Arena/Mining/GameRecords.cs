using ChessLab.Arena.Notation;
using ChessLab.Data;
using Microsoft.EntityFrameworkCore;

namespace ChessLab.Arena.Mining;

public static class GameRecords
{
    public static ChessLabDbContext Open(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ChessLabDbContext>();
        if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            options.UseSqlite(connectionString);
        else
            options.UseNpgsql(connectionString);

        return new ChessLabDbContext(options.Options);
    }

    public static async Task<IReadOnlyList<GameRecord>> FinishedBotGamesAsync(ChessLabDbContext db, DateTime since,
        CancellationToken ct)
    {
        var games = await db.GameRecords
            .Include(game => game.Players)
            .Where(game => game.FinishedAtUtc >= since)
            .Where(game => game.Players.Any(player => player.BotDifficulty != null))
            .OrderBy(game => game.FinishedAtUtc)
            .ToListAsync(ct);

        return [.. games.Where(game => Modes.IsReplayable(game.Kind))];
    }
}
