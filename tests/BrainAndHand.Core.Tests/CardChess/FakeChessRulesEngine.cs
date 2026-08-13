using BrainAndHand.Core.Chess;

namespace BrainAndHand.Core.Tests.CardChess;

/// <summary>
/// A fully-controllable <see cref="IChessRulesEngine"/> test double. Some GameState scenarios (most
/// notably Emergency Move — see GameStateTests) are hard or impossible to reach via genuinely legal
/// chess positions, because Card Chess's 13 cards give complete piece-type/pawn-file coverage: if
/// any piece anywhere can resolve a check, its card will find it. This double lets GameState's own
/// orchestration (the draw/skip loop, emergency activation, HP bookkeeping) be tested in isolation
/// from whether a given "no card works" scenario is realistic, exactly the sort of substitution
/// <see cref="IChessRulesEngine"/> exists to allow (see its own doc comment).
/// </summary>
internal sealed class FakeChessRulesEngine : IChessRulesEngine
{
    public Side SideToMove { get; set; } = Side.White;
    public GameEndResult? EndResult { get; set; }
    public List<ChessMove> AllMoves { get; set; } = [];
    public Dictionary<PieceKind, List<ChessMove>> MovesByKind { get; } = new();
    public List<ChessMove> AppliedMoves { get; } = [];
    public Side? Resigned { get; private set; }
    public Side? TimedOut { get; private set; }
    public string Fen { get; set; } = "8/8/8/8/8/8/8/8 w - - 0 1";
    public bool InCheck { get; set; } = true;

    public bool IsInCheck(Side side) => InCheck;

    public IReadOnlyList<ChessMove> LegalMoves() => AllMoves;

    public IReadOnlyList<ChessMove> LegalMoves(PieceKind kind) =>
        MovesByKind.TryGetValue(kind, out var moves) ? moves : [];

    // Toggles sides like a real engine would — matters for tests that make several moves in a row,
    // since GameState only re-evaluates a side's own situation (e.g. "still no playable card, and
    // now 0 HP") once it's genuinely that side's turn again, not immediately after its own move.
    public void ApplyMove(ChessMove move)
    {
        AppliedMoves.Add(move);
        SideToMove = SideToMove == Side.White ? Side.Black : Side.White;
    }

    public void Resign(Side side) => Resigned = side;

    public void DeclareTimeout(Side side) => TimedOut = side;

    public string ToFen() => Fen;
}
