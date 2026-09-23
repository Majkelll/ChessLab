using ChessLab.Core.Chess;

namespace ChessLab.Core.Board;

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

public readonly record struct BoardPiece(Side Side, MovePower Powers, bool IsRoyal)
{
    public static BoardPiece Of(Side side, PieceKind kind) =>
        new(side, PowerOf(kind), kind == PieceKind.King);

    public bool Has(MovePower power) => (Powers & power) != 0;

    public BoardPiece With(MovePower added) => this with { Powers = Powers | added };

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
