using ChessLab.Core.Chess;

namespace ChessLab.Web.Client.Components;

public static class PieceKindDisplay
{
    public static string Glyph(PieceKind kind) => kind switch
    {
        PieceKind.Pawn => "♟",
        PieceKind.Knight => "♞",
        PieceKind.Bishop => "♝",
        PieceKind.Rook => "♜",
        PieceKind.Queen => "♛",
        PieceKind.King => "♚",
        _ => "?",
    };

    public static string Label(PieceKind kind) => kind switch
    {
        PieceKind.Pawn => "Pawn",
        PieceKind.Knight => "Knight",
        PieceKind.Bishop => "Bishop",
        PieceKind.Rook => "Rook",
        PieceKind.Queen => "Queen",
        PieceKind.King => "King",
        _ => kind.ToString(),
    };
}
