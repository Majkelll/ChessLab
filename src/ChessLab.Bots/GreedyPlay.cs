using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

/// <summary>
/// Move choice for the modes no chess engine understands. It looks exactly one move ahead: take the
/// most valuable thing on offer, otherwise do something harmless. Difficulty decides how often it
/// bothers — an easy bot mostly plays at random, an expert one always takes the best it can see.
/// </summary>
internal static class GreedyPlay
{
    public static int ValueOf(PieceKind kind) => kind switch
    {
        PieceKind.Pawn => 1,
        PieceKind.Knight => 3,
        PieceKind.Bishop => 3,
        PieceKind.Rook => 5,
        PieceKind.Queen => 9,
        PieceKind.King => 100,
        _ => 0,
    };

    public static ChessMove Choose(IReadOnlyList<ChessMove> moves, BotDifficulty difficulty, Random rng,
        Func<ChessMove, int>? bonus = null)
    {
        if (moves.Count == 0)
            throw new InvalidOperationException("There is no move to choose from.");

        if (difficulty == BotDifficulty.Easy || (difficulty == BotDifficulty.Medium && rng.Next(3) == 0))
            return moves[rng.Next(moves.Count)];

        var best = int.MinValue;
        var candidates = new List<ChessMove>();

        foreach (var move in moves)
        {
            var score = Score(move) + (bonus?.Invoke(move) ?? 0);
            if (score > best)
            {
                best = score;
                candidates.Clear();
            }

            if (score == best)
                candidates.Add(move);
        }

        return candidates[rng.Next(candidates.Count)];
    }

    private static int Score(ChessMove move)
    {
        var score = move.CapturedPiece is { } captured ? 10 * ValueOf(captured) : 0;
        if (move.PromoteTo is { } promotion)
            score += 8 * ValueOf(promotion);

        return score;
    }
}
