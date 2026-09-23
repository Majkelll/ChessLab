using ChessLab.Core.Chess;

namespace ChessLab.Core.Board;

/// <summary>What a piece is allowed to do, as a set rather than a single kind: Absorption Chess
/// stacks the powers of everything a piece has captured onto it, so one piece can move as a knight
/// and a rook at once. Ordinary chess pieces carry exactly one of these.</summary>
[Flags]
public enum MovePower
{
    None = 0,
    Pawn = 1,
    Knight = 2,
    Bishop = 4,
    Rook = 8,
    Queen = 16,
    King = 32,
}

/// <summary><see cref="IsRoyal"/> is kept apart from <see cref="MovePower.King"/> because the two
/// aren't the same thing: a piece that absorbed a king's moves still isn't the piece whose capture
/// loses the game.</summary>
public readonly record struct BoardPiece(Side Side, MovePower Powers, bool IsRoyal)
{
    public static BoardPiece Of(Side side, PieceKind kind) =>
        new(side, PowerOf(kind), kind == PieceKind.King);

    public bool Has(MovePower power) => (Powers & power) != 0;

    public BoardPiece With(MovePower added) => this with { Powers = Powers | added };

    /// <summary>The kind this piece is named and drawn as — its most valuable power, except that a
    /// royal piece always reads as a king.</summary>
    public PieceKind PrimaryKind
    {
        get
        {
            if (IsRoyal)
                return PieceKind.King;
            if (Has(MovePower.Queen))
                return PieceKind.Queen;
            if (Has(MovePower.Rook))
                return PieceKind.Rook;
            if (Has(MovePower.Bishop))
                return PieceKind.Bishop;
            if (Has(MovePower.Knight))
                return PieceKind.Knight;
            if (Has(MovePower.King))
                return PieceKind.King;
            return PieceKind.Pawn;
        }
    }

    public static MovePower PowerOf(PieceKind kind) => kind switch
    {
        PieceKind.Pawn => MovePower.Pawn,
        PieceKind.Knight => MovePower.Knight,
        PieceKind.Bishop => MovePower.Bishop,
        PieceKind.Rook => MovePower.Rook,
        PieceKind.Queen => MovePower.Queen,
        PieceKind.King => MovePower.King,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown piece kind."),
    };
}

[Flags]
public enum CastlingRights
{
    None = 0,
    WhiteKingSide = 1,
    WhiteQueenSide = 2,
    BlackKingSide = 4,
    BlackQueenSide = 8,
    All = WhiteKingSide | WhiteQueenSide | BlackKingSide | BlackQueenSide,
}
