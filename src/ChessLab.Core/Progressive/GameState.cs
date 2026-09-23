using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.Progressive;

public sealed class GameState : IGameEngineState
{
    public const int SeriesLimit = 150;

    private readonly IChessRulesEngine engine;
    private readonly List<ChessMove> moveHistory = [];
    private readonly List<string> notations = [];
    private GameEndResult? forcedResult;

    public GameState(IChessRulesEngine engine, Clock clock)
    {
        this.engine = engine;
        Clock = clock;
    }

    public Clock Clock { get; }

    public int SeriesNumber { get; private set; } = 1;

    public int MovesPlayedInSeries { get; private set; }

    public int MovesLeftInSeries => SeriesNumber - MovesPlayedInSeries;

    public Side SideToMove => engine.SideToMove;

    public GameEndResult? EndResult => engine.EndResult ?? forcedResult;

    public bool IsGameOver => EndResult is not null;

    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;

    public IReadOnlyList<string> MoveNotations => notations;

    public string PositionText => engine.ToFen();

    public IReadOnlyList<Side> SidesOnTheClock => [SideToMove];

    public IReadOnlyList<ChessMove> AvailableMoves => IsGameOver ? [] : engine.LegalMoves();

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, TimeSpan elapsed)
    {
        EnsureNotOver();

        var mover = SideToMove;
        ChessMove? match = null;
        foreach (var candidate in AvailableMoves)
        {
            if (candidate.From == from && candidate.To == to && candidate.PromoteTo == promoteTo)
            {
                match = candidate;
                break;
            }
        }

        if (match is null)
            throw new InvalidOperationException($"{from}-{to} is not a legal move right now.");

        var applied = match.Value;
        engine.ApplyMove(applied);
        moveHistory.Add(applied);
        notations.Add(MovesPlayedInSeries == 0 ? $"{SeriesNumber}.{applied.San}" : applied.San);
        MovesPlayedInSeries++;

        Clock.Deduct(mover, elapsed);

        if (applied.IsCheck || MovesPlayedInSeries >= SeriesNumber || !ContinueSeries(mover))
            EndSeries(mover);

        return applied;
    }

    private bool ContinueSeries(Side mover)
    {
        if (IsGameOver)
            return false;

        var afterMove = engine.ToFen();
        engine.LoadPosition(FenBoard.WithSideToMove(afterMove, mover));

        if (engine.LegalMoves().Count > 0)
            return true;

        engine.LoadPosition(afterMove);
        return false;
    }

    private void EndSeries(Side mover)
    {
        if (!IsGameOver)
            Clock.ApplyIncrement(mover);

        SeriesNumber++;
        MovesPlayedInSeries = 0;

        if (!IsGameOver && SeriesNumber > SeriesLimit)
            forcedResult = new GameEndResult(GameEndReason.MoveLimit, null);
    }

    public void Resign(Side side)
    {
        EnsureNotOver();
        engine.Resign(side);
    }

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
}
