using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Draft;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Engines;

public static class MaterialEvaluation
{
    public const int Win = 100_000;

    public static int Of(IRoomSession session)
    {
        var state = session.ToStateDto(null);
        if (state.IsGameOver)
            return state.Winner switch
            {
                Side.White => Win,
                Side.Black => -Win,
                _ => 0,
            };

        return state.Kind switch
        {
            GameKind.MartianChess => 100 * (state.Martian!.WhiteScore - state.Martian.BlackScore) + state.PositionText.Sum(letter => letter) % 50,
            GameKind.DraftChess when state.Draft!.Phase != DraftPhase.Playing => Picks(state.Draft),
            GameKind.AliceChess => Fen(state.Alice!.BoardAFen) + Fen(state.Alice.BoardBFen),
            GameKind.AbsorptionChess => Fen(state.Absorption!.Fen),
            _ => Fen(state.PositionText),
        };
    }

    public static int ValueOf(PieceKind kind) => kind switch
    {
        PieceKind.Pawn => 100,
        PieceKind.Knight => 300,
        PieceKind.Bishop => 300,
        PieceKind.Rook => 500,
        PieceKind.Queen => 900,
        _ => 0,
    };

    private static int Picks(DraftSectionDto draft) =>
        draft.WhitePicks.Sum(ValueOf) - draft.BlackPicks.Sum(ValueOf);

    private static int Fen(string fen)
    {
        var board = fen.Split(' ')[0];
        var total = 0;
        foreach (var letter in board)
        {
            var value = char.ToLowerInvariant(letter) switch
            {
                'p' => 100,
                'n' => 300,
                'b' => 300,
                'r' => 500,
                'q' => 900,
                _ => 0,
            };
            total += char.IsUpper(letter) ? value : -value;
        }

        return total;
    }
}
