using ChessLab.Core.Board;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.Absorption;

/// <summary>
/// Absorption Chess: whatever a piece captures, it becomes as well. A knight that takes a rook
/// moves as a knight and a rook from then on, and a pawn that takes a queen keeps its pawn step
/// too. The king absorbs like everything else but stays the piece whose safety decides the game, so
/// check, checkmate and stalemate work exactly as in ordinary chess — it's only what each piece can
/// do that grows.
/// </summary>
public sealed class GameState : IGameEngineState
{
    public const int MoveLimit = 300;

    private readonly PieceBoard board;
    private readonly List<ChessMove> moveHistory = [];
    private readonly List<string> notations = [];
    private readonly Dictionary<string, int> positionCounts = [];
    private IReadOnlyList<ChessMove>? availableMoves;
    private GameEndResult? result;

    public GameState(Clock clock, PieceBoard? board = null)
    {
        Clock = clock;
        this.board = board ?? PieceBoard.StandardStart();
        this.board.AbsorbOnCapture = true;
        CountPosition();
    }

    public Clock Clock { get; }

    public Side SideToMove => board.SideToMove;

    public GameEndResult? EndResult => result;

    public bool IsGameOver => result is not null;

    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;

    public IReadOnlyList<string> MoveNotations => notations;

    public string Fen => board.ToFen();

    /// <summary>Which pieces carry powers beyond their own, as "e4:RN" entries.</summary>
    public string PowersText => board.PowersText();

    public string PositionText => PowersText.Length == 0 ? Fen : $"{Fen} | {PowersText}";

    public IReadOnlyList<Side> SidesOnTheClock => [SideToMove];

    public IReadOnlyList<ChessMove> AvailableMoves =>
        availableMoves ??= IsGameOver ? [] : board.LegalMoves(SideToMove);

    public bool IsInCheck(Side side) => board.IsRoyalAttacked(side);

    /// <summary>Every power the piece on <paramref name="square"/> has, so the UI can show what a
    /// piece has eaten rather than only what it started as.</summary>
    public MovePower PowersAt(Square square) => board.At(square)?.Powers ?? MovePower.None;

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

        var applied = board.Apply(match.Value);
        moveHistory.Add(applied);
        notations.Add(applied.San);
        availableMoves = null;

        Clock.Deduct(mover, elapsed);
        Clock.ApplyIncrement(mover);

        CountPosition();
        DecideOutcome();
        return applied;
    }

    private void CountPosition()
    {
        var key = $"{Fen.Split(' ')[0]} {SideToMove} {PowersText}";
        positionCounts[key] = positionCounts.GetValueOrDefault(key) + 1;

        if (positionCounts[key] >= 3)
            result = new GameEndResult(GameEndReason.Repetition, null);
    }

    private void DecideOutcome()
    {
        if (IsGameOver)
            return;

        if (AvailableMoves.Count == 0)
        {
            result = IsInCheck(SideToMove)
                ? new GameEndResult(GameEndReason.Checkmate, Opponent(SideToMove))
                : new GameEndResult(GameEndReason.Stalemate, null);
            return;
        }

        if (board.HalfmoveClock >= 100)
            result = new GameEndResult(GameEndReason.FiftyMoveRule, null);
        else if (moveHistory.Count >= MoveLimit)
            result = new GameEndResult(GameEndReason.MoveLimit, null);
    }

    public void Resign(Side side)
    {
        EnsureNotOver();
        result = new GameEndResult(GameEndReason.Resignation, Opponent(side));
    }

    public void DeclareTimeoutIfFlagged()
    {
        if (IsGameOver)
            return;

        if (Clock.IsFlagged(SideToMove))
            result = new GameEndResult(GameEndReason.Timeout, Opponent(SideToMove));
    }

    private static Side Opponent(Side side) => side == Side.White ? Side.Black : Side.White;

    private void EnsureNotOver()
    {
        if (IsGameOver)
            throw new InvalidOperationException("The game has already ended.");
    }
}
