namespace ChessLab.Core.Chess;

public interface IChessRulesEngine
{
    Side SideToMove { get; }
    bool IsInCheck(Side side);

    GameEndResult? EndResult { get; }

    IReadOnlyList<ChessMove> LegalMoves();

    IReadOnlyList<ChessMove> LegalMoves(PieceKind kind);

    bool HasLegalMove(Side side, PieceKind kind);

    void ApplyMove(ChessMove move);

    void Resign(Side side);
    void DeclareTimeout(Side side);

    string ToFen();

    void LoadPosition(string fen);
}
