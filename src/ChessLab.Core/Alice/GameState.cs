using ChessLab.Core.Board;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.Alice;

/// <summary>
/// Alice Chess (Lewis Carroll, 1953): the game runs on two boards at once. A move has to be legal
/// on the board the piece is standing on, the square it lands on has to be empty on the
/// <em>other</em> board, and the piece then vanishes through the looking-glass and reappears on
/// that other board. Check is whatever threatens your king on the board it happens to be on.
/// Castling is off here, and en passant can't come up at all — a pawn that just stepped two squares
/// is on the far board by the time anyone could take it.
/// </summary>
public sealed class GameState : IGameEngineState
{
    /// <summary>Repetition is hard to pin down across two boards and nobody's counting halfmoves,
    /// so an endless game is called off instead.</summary>
    public const int MoveLimit = 300;

    private readonly PieceBoard[] boards;
    private readonly List<ChessMove> moveHistory = [];
    private readonly List<string> notations = [];
    private IReadOnlyList<ChessMove>? availableMoves;
    private GameEndResult? result;

    public GameState(Clock clock, PieceBoard? boardA = null, PieceBoard? boardB = null)
    {
        Clock = clock;
        boards = [boardA ?? PieceBoard.StandardStart(), boardB ?? PieceBoard.Empty()];

        foreach (var board in boards)
        {
            board.RevokeCastling(CastlingRights.All);
            board.ClearEnPassant();
        }
    }

    public Clock Clock { get; }

    public Side SideToMove { get; private set; } = Side.White;

    public GameEndResult? EndResult => result;

    public bool IsGameOver => result is not null;

    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;

    public IReadOnlyList<string> MoveNotations => notations;

    /// <summary>Both boards, since neither on its own is the position.</summary>
    public string PositionText => $"{FenOf(0)} | {FenOf(1)}";

    public IReadOnlyList<Side> SidesOnTheClock => [SideToMove];

    public string FenOf(int board) => boards[board].ToFen();

    /// <summary>Which board the piece on <paramref name="square"/> stands on, or null when that
    /// square is empty on both — a square is never occupied on both at once.</summary>
    public int? BoardOf(Square square) =>
        boards[0].At(square) is not null ? 0 : boards[1].At(square) is not null ? 1 : null;

    public IReadOnlyList<ChessMove> AvailableMoves => availableMoves ??= LegalMovesFor(SideToMove);

    public bool IsInCheck(Side side) => IsInCheck(boards, side);

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

        var origin = BoardOf(from)!.Value;
        var applied = Play(boards, match.Value);
        moveHistory.Add(applied);
        notations.Add($"{applied.San}({BoardLabel(origin)})");

        Clock.Deduct(mover, elapsed);
        Clock.ApplyIncrement(mover);

        SideToMove = Opponent(mover);
        availableMoves = null;

        DecideOutcome();
        return applied;
    }

    private void DecideOutcome()
    {
        if (AvailableMoves.Count == 0)
        {
            result = IsInCheck(SideToMove)
                ? new GameEndResult(GameEndReason.Checkmate, Opponent(SideToMove))
                : new GameEndResult(GameEndReason.Stalemate, null);
            return;
        }

        if (moveHistory.Count >= MoveLimit)
            result = new GameEndResult(GameEndReason.MoveLimit, null);
    }

    private IReadOnlyList<ChessMove> LegalMovesFor(Side side)
    {
        var legal = new List<ChessMove>();

        for (var index = 0; index < boards.Length; index++)
        {
            var board = boards[index];
            var other = boards[1 - index];

            foreach (var move in board.PseudoLegalMoves(side))
            {
                if (move.CapturedPiece == PieceKind.King || other.At(move.To) is not null)
                    continue;

                var probe = new[] { boards[0].Clone(), boards[1].Clone() };
                Play(probe, move);

                if (!IsInCheck(probe, side))
                    legal.Add(move);
            }
        }

        return legal;
    }

    /// <summary>Plays the move on the board the piece stands on, then sends the piece through to the
    /// other board — which is the whole move, not an afterthought to it.</summary>
    private static ChessMove Play(PieceBoard[] state, ChessMove move)
    {
        var index = state[0].At(move.From) is not null ? 0 : 1;
        var board = state[index];
        var other = state[1 - index];

        var applied = board.Apply(move, annotate: false);
        var arrived = board.At(move.To)!.Value;
        board.Set(move.To, null);
        other.Set(move.To, arrived);

        board.ClearEnPassant();
        other.ClearEnPassant();
        return applied;
    }

    private static bool IsInCheck(PieceBoard[] state, Side side)
    {
        for (var index = 0; index < state.Length; index++)
        {
            if (state[index].RoyalSquare(side) is { } royal)
                return state[index].IsAttacked(royal, Opponent(side));
        }

        return false;
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

    private static string BoardLabel(int board) => board == 0 ? "A" : "B";

    private static Side Opponent(Side side) => side == Side.White ? Side.Black : Side.White;

    private void EnsureNotOver()
    {
        if (IsGameOver)
            throw new InvalidOperationException("The game has already ended.");
    }
}
