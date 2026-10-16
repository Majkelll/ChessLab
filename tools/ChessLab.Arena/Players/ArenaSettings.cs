namespace ChessLab.Arena.Players;

public sealed record ArenaSettings(string StockfishPath = "stockfish", TimeSpan? BudgetOverride = null)
{
    public static readonly TimeSpan DefaultBudget = TimeSpan.FromMilliseconds(100);
}
