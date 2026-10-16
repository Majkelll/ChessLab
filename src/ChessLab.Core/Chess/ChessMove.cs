namespace ChessLab.Core.Chess;

public readonly record struct ChessMove(
    Square From,
    Square To,
    PieceKind Piece,
    PieceKind? CapturedPiece,
    PieceKind? PromoteTo,
    bool IsCheck,
    bool IsCheckmate,
    string San)
{
    public override string ToString() => San;
}
