using ChessLab.Core.Board;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.Draft;

public enum DraftPhase
{
    Drafting,
    Placing,
    Playing,
}

/// <summary>
/// Draft Chess: the armies are built before a piece ever moves. Both sides spend a points budget
/// picking from one shared pool in snake order — pick, opponent picks twice, you pick twice, and so
/// on — and passing puts you out of the draft for good. Each side then lays its army out on its own
/// two ranks in secret, king included, pawns on the second rank only, and the game that follows is
/// ordinary chess with no castling, since nobody's king starts on e1.
/// </summary>
public sealed class GameState : IGameEngineState
{
    public const int Budget = 39;
    public const int MaxPawns = 8;

    /// <summary>Everything that isn't a pawn shares the back rank with the king, so seven is all
    /// there is room for — which also means a legal draft can always be laid out.</summary>
    public const int MaxOfficers = 7;
    public const int MoveLimit = 300;

    private static readonly IReadOnlyDictionary<PieceKind, int> Costs = new Dictionary<PieceKind, int>
    {
        [PieceKind.Queen] = 9,
        [PieceKind.Rook] = 5,
        [PieceKind.Bishop] = 3,
        [PieceKind.Knight] = 3,
        [PieceKind.Pawn] = 1,
    };

    private static readonly IReadOnlyDictionary<PieceKind, int> PoolSize = new Dictionary<PieceKind, int>
    {
        [PieceKind.Queen] = 2,
        [PieceKind.Rook] = 4,
        [PieceKind.Bishop] = 4,
        [PieceKind.Knight] = 4,
        [PieceKind.Pawn] = 16,
    };

    private readonly Dictionary<PieceKind, int> pool;
    private readonly Dictionary<Side, List<PieceKind>> picks;
    private readonly Dictionary<Side, bool> passed;
    private readonly Dictionary<Side, Dictionary<Square, PieceKind>> placements;
    private readonly List<ChessMove> moveHistory = [];
    private readonly List<string> notations = [];
    private readonly Dictionary<string, int> positionCounts = [];
    private PieceBoard? board;
    private IReadOnlyList<ChessMove>? availableMoves;
    private GameEndResult? result;
    private int pickSlot;

    public GameState(Clock clock)
    {
        Clock = clock;
        pool = PoolSize.ToDictionary(entry => entry.Key, entry => entry.Value);
        picks = new Dictionary<Side, List<PieceKind>> { [Side.White] = [], [Side.Black] = [] };
        passed = new Dictionary<Side, bool> { [Side.White] = false, [Side.Black] = false };
        placements = new Dictionary<Side, Dictionary<Square, PieceKind>>
        {
            [Side.White] = [],
            [Side.Black] = [],
        };
        AdvanceDraft();
    }

    public Clock Clock { get; }

    public DraftPhase Phase { get; private set; } = DraftPhase.Drafting;

    /// <summary>Whose pick it is, or null once the draft is done.</summary>
    public Side? SideToPick { get; private set; }

    public Side SideToMove => board?.SideToMove ?? Side.White;

    public GameEndResult? EndResult => result;

    public bool IsGameOver => result is not null;

    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;

    public IReadOnlyList<string> MoveNotations => notations;

    /// <summary>The drafted position, once there is one to play.</summary>
    public string? Fen => board?.ToFen();

    public string PositionText => Fen ?? DraftText();

    public IReadOnlyList<Side> SidesOnTheClock => Phase switch
    {
        DraftPhase.Drafting => SideToPick is { } side ? [side] : [],
        DraftPhase.Placing => [.. Sides.Where(side => !HasFinishedPlacing(side))],
        _ => [SideToMove],
    };

    public IReadOnlyList<ChessMove> AvailableMoves =>
        availableMoves ??= board is null || IsGameOver ? [] : board.LegalMoves(board.SideToMove);

    public IReadOnlyDictionary<PieceKind, int> Pool => pool;

    public IReadOnlyList<PieceKind> PicksOf(Side side) => picks[side];

    public bool HasPassed(Side side) => passed[side];

    public int SpentBy(Side side) => picks[side].Sum(kind => Costs[kind]);

    public int BudgetLeft(Side side) => Budget - SpentBy(side);

    public IReadOnlyDictionary<Square, PieceKind> PlacementsOf(Side side) => placements[side];

    /// <summary>A side is done laying out once its king and every piece it drafted is on the board.</summary>
    public bool HasFinishedPlacing(Side side) =>
        placements[side].Count == picks[side].Count + 1 && placements[side].ContainsValue(PieceKind.King);

    public static int CostOf(PieceKind kind) => Costs[kind];

    public void Pick(Side side, PieceKind kind)
    {
        EnsureNotOver();

        if (Phase != DraftPhase.Drafting)
            throw new InvalidOperationException("The draft is over.");

        if (SideToPick != side)
            throw new InvalidOperationException($"It is not {side}'s pick.");

        if (!CanPick(side, kind))
            throw new InvalidOperationException($"{side} cannot take a {kind} right now.");

        pool[kind]--;
        picks[side].Add(kind);
        pickSlot++;
        AdvanceDraft();
    }

    public void Pass(Side side)
    {
        EnsureNotOver();

        if (Phase != DraftPhase.Drafting)
            throw new InvalidOperationException("The draft is over.");

        if (SideToPick != side)
            throw new InvalidOperationException($"It is not {side}'s pick.");

        passed[side] = true;
        pickSlot++;
        AdvanceDraft();
    }

    public bool CanPick(Side side, PieceKind kind)
    {
        if (!Costs.TryGetValue(kind, out var cost) || pool.GetValueOrDefault(kind) == 0)
            return false;

        if (cost > BudgetLeft(side))
            return false;

        return kind == PieceKind.Pawn
            ? picks[side].Count(pick => pick == PieceKind.Pawn) < MaxPawns
            : picks[side].Count(pick => pick != PieceKind.Pawn) < MaxOfficers;
    }

    private bool CanPickAnything(Side side) =>
        !passed[side] && Costs.Keys.Any(kind => CanPick(side, kind));

    /// <summary>Snake order: White, Black, Black, White, White, and so on — a side that has passed
    /// or run out of budget is skipped over rather than stalling the draft.</summary>
    private void AdvanceDraft()
    {
        while (CanPickAnything(Side.White) || CanPickAnything(Side.Black))
        {
            var candidate = SnakeSide(pickSlot);
            if (CanPickAnything(candidate))
            {
                SideToPick = candidate;
                return;
            }

            pickSlot++;
        }

        SideToPick = null;
        Phase = DraftPhase.Placing;
    }

    private static Side SnakeSide(int slot) => (slot + 1) / 2 % 2 == 0 ? Side.White : Side.Black;

    public void Place(Side side, PieceKind kind, Square square)
    {
        EnsureNotOver();

        if (Phase != DraftPhase.Placing)
            throw new InvalidOperationException("Pieces can only be laid out once the draft is over.");

        if (!HomeRanks(side).Contains(square.Rank))
            throw new InvalidOperationException($"{side} can only use its own two ranks.");

        if (square.Rank != (kind == PieceKind.Pawn ? PawnRank(side) : BackRank(side)))
        {
            throw new InvalidOperationException(kind == PieceKind.Pawn
                ? "Pawns go on the second rank."
                : $"A {kind} goes on the back rank.");
        }

        if (placements[side].ContainsKey(square))
            throw new InvalidOperationException($"{square} is already taken.");

        if (Remaining(side, kind) == 0)
            throw new InvalidOperationException($"{side} has no {kind} left to place.");

        placements[side][square] = kind;

        if (Sides.All(HasFinishedPlacing))
            StartPlay();
    }

    public void Unplace(Side side, Square square)
    {
        EnsureNotOver();

        if (Phase != DraftPhase.Placing)
            throw new InvalidOperationException("The layout is already locked in.");

        if (!placements[side].Remove(square))
            throw new InvalidOperationException($"{side} has nothing on {square}.");
    }

    /// <summary>How many of <paramref name="kind"/> this side still has waiting to be laid out.</summary>
    public int Remaining(Side side, PieceKind kind)
    {
        var owned = kind == PieceKind.King ? 1 : picks[side].Count(pick => pick == kind);
        return owned - placements[side].Values.Count(placed => placed == kind);
    }

    private void StartPlay()
    {
        var built = PieceBoard.Empty();

        foreach (var side in Sides)
        {
            foreach (var (square, kind) in placements[side])
                built.Set(square, BoardPiece.Of(side, kind));
        }

        board = built;
        board.SetSideToMove(Side.White);
        Phase = DraftPhase.Playing;

        // Anything that asked for the moves while the armies were still being built got an empty
        // list, and that answer is cached — there is a real board to read now.
        availableMoves = null;

        CountPosition();
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, TimeSpan elapsed)
    {
        EnsureNotOver();

        if (Phase != DraftPhase.Playing || board is null)
            throw new InvalidOperationException("The armies are still being built.");

        var mover = board.SideToMove;
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
        var key = board!.ToFen().Split(' ')[0] + board.SideToMove;
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
            result = board!.IsRoyalAttacked(board.SideToMove)
                ? new GameEndResult(GameEndReason.Checkmate, Opponent(board.SideToMove))
                : new GameEndResult(GameEndReason.Stalemate, null);
            return;
        }

        if (board!.HalfmoveClock >= 100)
            result = new GameEndResult(GameEndReason.FiftyMoveRule, null);
        else if (moveHistory.Count >= MoveLimit)
            result = new GameEndResult(GameEndReason.MoveLimit, null);
    }

    private string DraftText() =>
        $"{Phase} W:{string.Join(',', picks[Side.White])} B:{string.Join(',', picks[Side.Black])}";

    private static IEnumerable<Side> Sides => [Side.White, Side.Black];

    private static int[] HomeRanks(Side side) => side == Side.White ? [0, 1] : [7, 6];

    private static int PawnRank(Side side) => side == Side.White ? 1 : 6;

    private static int BackRank(Side side) => side == Side.White ? 0 : 7;

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

    private static Side Opponent(Side side) => side == Side.White ? Side.Black : Side.White;

    private void EnsureNotOver()
    {
        if (IsGameOver)
            throw new InvalidOperationException("The game has already ended.");
    }
}
