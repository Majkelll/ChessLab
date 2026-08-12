using BrainAndHand.Core.Chess;

namespace BrainAndHand.Bots;

internal static class UciNotation
{
    public static string ToUci(this ChessMove move)
    {
        var promotion = move.PromoteTo switch
        {
            PieceKind.Queen => "q",
            PieceKind.Rook => "r",
            PieceKind.Bishop => "b",
            PieceKind.Knight => "n",
            _ => "",
        };
        return $"{move.From}{move.To}{promotion}";
    }

    public static (Square From, Square To, PieceKind? Promotion) ParseUciMove(string uci)
    {
        var from = Square.Parse(uci[..2]);
        var to = Square.Parse(uci.Substring(2, 2));
        PieceKind? promotion = uci.Length >= 5
            ? uci[4] switch
            {
                'q' => PieceKind.Queen,
                'r' => PieceKind.Rook,
                'b' => PieceKind.Bishop,
                'n' => PieceKind.Knight,
                _ => null,
            }
            : null;

        return (from, to, promotion);
    }
}
