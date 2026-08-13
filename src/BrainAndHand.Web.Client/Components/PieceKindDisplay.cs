using BrainAndHand.Core.Chess;

namespace BrainAndHand.Web.Client.Components;

/// <summary>Shared glyph/label rendering for a piece kind — used by the Brain's selectable
/// <see cref="PieceKindCards"/> and anywhere else a piece kind needs to be shown the same way.</summary>
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
