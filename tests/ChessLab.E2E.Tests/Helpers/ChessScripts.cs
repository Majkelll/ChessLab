using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.E2E.Tests.Helpers;

/// <summary>Deterministic move sequences played through the real UI, for tests that need a specific
/// board outcome (e.g. checkmate) without depending on a bot's unpredictable choices.</summary>
public static class ChessScripts
{
    /// <summary>Plays one full turn: the Brain announces <paramref name="kind"/>, then the Hand moves
    /// <paramref name="from"/>-&gt;<paramref name="to"/>, waiting in between so the two (possibly
    /// different-page) actors never act on stale state.</summary>
    public static async Task PlayTurnAsync(StartedGame game, Side side, PieceKind kind, string from, string to)
    {
        var brain = game[side, SeatRole.Brain];
        var hand = game[side, SeatRole.Hand];

        await brain.WaitForTurnTextAsync("Brain is announcing a piece");
        await brain.SelectPieceKindAsync(kind);

        await hand.WaitForTurnTextAsync("Hand is making a move");
        await hand.MoveAsync(from, to);
    }

    /// <summary>The fastest possible checkmate: 1. f3 e5 2. g4 Qh4# — White gets mated on move 2.</summary>
    public static async Task PlayFoolsMateAsync(StartedGame game)
    {
        await PlayTurnAsync(game, Side.White, PieceKind.Pawn, "f2", "f3");
        await PlayTurnAsync(game, Side.Black, PieceKind.Pawn, "e7", "e5");
        await PlayTurnAsync(game, Side.White, PieceKind.Pawn, "g2", "g4");
        await PlayTurnAsync(game, Side.Black, PieceKind.Queen, "d8", "h4");
    }
}
