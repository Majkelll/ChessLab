using ChessLab.Core.Board;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.Bidding;

public enum BiddingPhase
{
    Bidding,
    Moving,
}

/// <summary>
/// Bidding Chess (Richman chess): there is no turn order. Before every move both sides secretly bid
/// some of their chips, the higher bid wins the right to move and pays what it bid to the loser, and
/// a tie goes to whoever holds the tiebreak marker, which then changes hands. Because a side can win
/// several bids in a row, check and checkmate don't exist here — the game is won by capturing the
/// opposing king outright.
/// </summary>
public sealed class GameState : IGameEngineState
{
    public const int StartingChips = 100;

    /// <summary>Without check or mate there is no natural draw here, so a game that neither side
    /// can finish is called off once both sides have had this many moves between them.</summary>
    public const int MoveLimit = 300;

    private readonly PieceBoard board;
    private readonly List<string> notations = [];
    private readonly List<ChessMove> moveHistory = [];
    private readonly Dictionary<Side, int> chips;
    private readonly Dictionary<Side, int?> pendingBids;
    private GameEndResult? result;

    public GameState(Clock clock, PieceBoard? board = null)
    {
        Clock = clock;
        this.board = board ?? PieceBoard.StandardStart();
        chips = new Dictionary<Side, int> { [Side.White] = StartingChips, [Side.Black] = StartingChips };
        pendingBids = new Dictionary<Side, int?> { [Side.White] = null, [Side.Black] = null };
    }

    public Clock Clock { get; }

    public BiddingPhase Phase { get; private set; } = BiddingPhase.Bidding;

    public Side SideToMove { get; private set; } = Side.White;

    /// <summary>Who wins a tied bid — and hands the marker over by doing so.</summary>
    public Side MarkerHolder { get; private set; } = Side.White;

    public int? LastWhiteBid { get; private set; }

    public int? LastBlackBid { get; private set; }

    public GameEndResult? EndResult => result;

    public bool IsGameOver => result is not null;

    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;

    public IReadOnlyList<string> MoveNotations => notations;

    public string PositionText => board.ToFen();

    public IReadOnlyList<Side> SidesOnTheClock => Phase == BiddingPhase.Bidding
        ? [.. Sides.Where(side => pendingBids[side] is null)]
        : [SideToMove];

    public int ChipsOf(Side side) => chips[side];

    public bool HasBid(Side side) => pendingBids[side] is not null;

    /// <summary>Everything the side that won the bid may play — every move the pieces can physically
    /// make, including ones that leave its own king attacked and ones that take the opposing king.</summary>
    public IReadOnlyList<ChessMove> AvailableMoves =>
        Phase == BiddingPhase.Moving && !IsGameOver ? MovesFor(SideToMove) : [];

    /// <summary>What a side could play if it won the bid — worth knowing while bids are still open,
    /// since that's the whole question being bid on.</summary>
    public IReadOnlyList<ChessMove> MovesFor(Side side) => IsGameOver ? [] : board.PseudoLegalMoves(side);

    public void SubmitBid(Side side, int amount, TimeSpan elapsed)
    {
        EnsureNotOver();

        if (Phase == BiddingPhase.Moving)
            throw new InvalidOperationException("Bids are closed until this move has been played.");

        if (pendingBids[side] is not null)
            throw new InvalidOperationException($"{side} has already bid this turn.");

        if (amount < 0 || amount > chips[side])
            throw new InvalidOperationException($"{side} can bid between 0 and {chips[side]} chips.");

        pendingBids[side] = amount;
        Clock.Deduct(side, elapsed);

        if (pendingBids[Side.White] is { } white && pendingBids[Side.Black] is { } black)
            ResolveBids(white, black);
    }

    private void ResolveBids(int white, int black)
    {
        LastWhiteBid = white;
        LastBlackBid = black;
        pendingBids[Side.White] = null;
        pendingBids[Side.Black] = null;

        Side winner;
        if (white != black)
        {
            winner = white > black ? Side.White : Side.Black;
        }
        else
        {
            winner = MarkerHolder;
            MarkerHolder = Opponent(MarkerHolder);
        }

        var paid = winner == Side.White ? white : black;
        chips[winner] -= paid;
        chips[Opponent(winner)] += paid;

        SideToMove = winner;
        board.SetSideToMove(winner);
        Phase = BiddingPhase.Moving;

        if (board.PseudoLegalMoves(winner).Count == 0)
            result = new GameEndResult(GameEndReason.Stalemate, null);
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, TimeSpan elapsed)
    {
        EnsureNotOver();

        if (Phase != BiddingPhase.Moving)
            throw new InvalidOperationException("Both sides have to bid before a move can be played.");

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
            throw new InvalidOperationException($"{from}-{to} is not a move {mover} can play right now.");

        var capturedKing = match.Value.CapturedPiece == PieceKind.King;
        var applied = board.Apply(match.Value, annotate: false);
        moveHistory.Add(applied);
        notations.Add($"{applied.San}({(mover == Side.White ? LastWhiteBid : LastBlackBid)})");

        Clock.Deduct(mover, elapsed);
        if (!capturedKing)
            Clock.ApplyIncrement(mover);

        Phase = BiddingPhase.Bidding;

        if (capturedKing)
            result = new GameEndResult(GameEndReason.KingCaptured, mover);
        else if (board.HalfmoveClock >= 100)
            result = new GameEndResult(GameEndReason.FiftyMoveRule, null);
        else if (moveHistory.Count >= MoveLimit)
            result = new GameEndResult(GameEndReason.MoveLimit, null);

        return applied;
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

        foreach (var side in SidesOnTheClock)
        {
            if (Clock.IsFlagged(side))
            {
                result = new GameEndResult(GameEndReason.Timeout, Opponent(side));
                return;
            }
        }
    }

    private static IEnumerable<Side> Sides => [Side.White, Side.Black];

    private static Side Opponent(Side side) => side == Side.White ? Side.Black : Side.White;

    private void EnsureNotOver()
    {
        if (IsGameOver)
            throw new InvalidOperationException("The game has already ended.");
    }
}
