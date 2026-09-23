using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.HandBrain;

/// <summary>
/// Orchestrates a Hand &amp; Brain chess game on top of an <see cref="IChessRulesEngine"/>:
/// each side's turn alternates between the Brain announcing a piece kind and the Hand
/// moving one of the pieces of that kind.
/// </summary>
public sealed class GameState : IGameEngineState
{
    private readonly IChessRulesEngine engine;
    private readonly List<ChessMove> moveHistory = [];

    public Clock Clock { get; }
    public TurnPhase Phase { get; private set; } = TurnPhase.BrainSelecting;
    public PieceKind? SelectedPieceKind { get; private set; }
    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;

    public Side SideToMove => engine.SideToMove;
    public GameEndResult? EndResult => engine.EndResult;
    public bool IsGameOver => EndResult is not null;

    public string ToFen() => engine.ToFen();

    public IReadOnlyList<string> MoveNotations => [.. MoveHistory.Select(move => move.San)];

    public string PositionText => ToFen();

    public GameState(IChessRulesEngine engine, Clock clock)
    {
        this.engine = engine;
        Clock = clock;
    }

    /// <summary>Piece kinds the Brain may currently announce (each has at least one legal move).</summary>
    public IReadOnlyList<PieceKind> AvailablePieceKinds()
    {
        EnsurePhase(TurnPhase.BrainSelecting);
        return engine.LegalMoves().Select(m => m.Piece).Distinct().ToArray();
    }

    public void SelectPieceKind(PieceKind kind)
    {
        EnsureNotOver();
        EnsurePhase(TurnPhase.BrainSelecting);

        if (!AvailablePieceKinds().Contains(kind))
            throw new InvalidOperationException($"{kind} has no legal move for {SideToMove}.");

        SelectedPieceKind = kind;
        Phase = TurnPhase.HandMoving;
    }

    /// <summary>Legal moves the Hand may currently choose from, restricted to the announced piece kind.</summary>
    public IReadOnlyList<ChessMove> AvailableMoves()
    {
        EnsurePhase(TurnPhase.HandMoving);
        return engine.LegalMoves(SelectedPieceKind!.Value);
    }

    /// <summary>
    /// Legal moves for a given piece kind, regardless of the current phase. Used by bots evaluating
    /// candidate announcements before actually committing to one via <see cref="SelectPieceKind"/>.
    /// </summary>
    public IReadOnlyList<ChessMove> LegalMovesFor(PieceKind kind) => engine.LegalMoves(kind);

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, TimeSpan elapsed)
    {
        EnsureNotOver();
        EnsurePhase(TurnPhase.HandMoving);

        var mover = SideToMove;
        ChessMove? match = null;
        foreach (var candidate in AvailableMoves())
        {
            if (candidate.From == from && candidate.To == to && candidate.PromoteTo == promoteTo)
            {
                match = candidate;
                break;
            }
        }

        if (match is null)
            throw new InvalidOperationException(
                $"{from}-{to} is not a legal move for the announced piece kind ({SelectedPieceKind}).");

        engine.ApplyMove(match.Value);
        moveHistory.Add(match.Value);

        Clock.Deduct(mover, elapsed);
        if (!IsGameOver)
            Clock.ApplyIncrement(mover);

        SelectedPieceKind = null;
        Phase = TurnPhase.BrainSelecting;

        return match.Value;
    }

    public void Resign(Side side)
    {
        EnsureNotOver();
        engine.Resign(side);
    }

    /// <summary>Call periodically from outside (e.g. a server-side timer) to end the game if a side's clock hit zero.</summary>
    public void DeclareTimeoutIfFlagged()
    {
        if (IsGameOver)
            return;

        if (Clock.IsFlagged(SideToMove))
            engine.DeclareTimeout(SideToMove);
    }

    private void EnsureNotOver()
    {
        if (IsGameOver)
            throw new InvalidOperationException("The game has already ended.");
    }

    private void EnsurePhase(TurnPhase expected)
    {
        if (Phase != expected)
            throw new InvalidOperationException($"Expected phase {expected} but was {Phase}.");
    }
}
