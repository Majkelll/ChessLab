using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.E2E.Tests.Helpers;

public static class ChessScripts
{
    public static async Task PlayTurnAsync(StartedGame game, Side side, PieceKind kind, string from, string to)
    {
        var brain = game[side, SeatRole.Brain];
        var hand = game[side, SeatRole.Hand];

        await brain.WaitForTurnTextAsync("Brain is announcing a piece");
        await brain.SelectPieceKindAsync(kind);

        await hand.WaitForTurnTextAsync("Hand is making a move");
        await hand.MoveAsync(from, to);
    }

    public static async Task PlayFoolsMateAsync(StartedGame game)
    {
        await PlayTurnAsync(game, Side.White, PieceKind.Pawn, "f2", "f3");
        await PlayTurnAsync(game, Side.Black, PieceKind.Pawn, "e7", "e5");
        await PlayTurnAsync(game, Side.White, PieceKind.Pawn, "g2", "g4");
        await PlayTurnAsync(game, Side.Black, PieceKind.Queen, "d8", "h4");
    }
}
