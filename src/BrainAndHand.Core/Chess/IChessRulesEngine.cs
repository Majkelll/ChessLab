namespace BrainAndHand.Core.Chess;

/// <summary>
/// Abstraction over the underlying chess rules library, so the Hand &amp; Brain domain
/// never depends on a specific chess implementation directly.
/// </summary>
public interface IChessRulesEngine
{
    Side SideToMove { get; }
    bool IsInCheck(Side side);

    /// <summary>Null while the game is still in progress.</summary>
    GameEndResult? EndResult { get; }

    /// <summary>All legal moves for the side to move.</summary>
    IReadOnlyList<ChessMove> LegalMoves();

    /// <summary>Legal moves for the side to move, restricted to a given piece kind.</summary>
    IReadOnlyList<ChessMove> LegalMoves(PieceKind kind);

    /// <summary>Applies a move previously obtained from <see cref="LegalMoves()"/>.</summary>
    void ApplyMove(ChessMove move);

    void Resign(Side side);
    void DeclareTimeout(Side side);

    string ToFen();
}
