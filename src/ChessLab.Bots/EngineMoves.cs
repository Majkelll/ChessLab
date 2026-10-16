using ChessLab.Core.Chess;

namespace ChessLab.Bots;

internal static class EngineMoves
{
    public static async Task<ChessMove> BestAsync(IChessEngine engine, string fen, IReadOnlyList<ChessMove> moves,
        DifficultyPreset preset, CancellationToken ct)
    {
        var candidates = moves.Select(move => move.ToUci()).Distinct().ToArray();
        var (bestUci, _) = await engine.GoAsync(fen, candidates, preset.Elo, preset.MovetimeMs, ct);
        var (from, to, promotion) = UciNotation.ParseUciMove(bestUci);

        return moves.First(move => move.From == from && move.To == to && move.PromoteTo == promotion);
    }
}
