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

public sealed class GameState : IGameEngineState
{
    public const int Budget = 24;
    public const int MaxPawns = 8;

    public const int MaxOfficers = 7;
    public const int MoveLimit = 300;
    public const int SecondsPerUnspentPoint = 20;

    private static readonly IReadOnlyDictionary<PieceKind, int> BasePrices = new Dictionary<PieceKind, int>
    {
        [PieceKind.Queen] = 9,
        [PieceKind.Rook] = 5,
        [PieceKind.Bishop] = 3,
        [PieceKind.Knight] = 3,
        [PieceKind.Pawn] = 1,
    };

    private static readonly IReadOnlyDictionary<PieceKind, int> PriceRise = new Dictionary<PieceKind, int>
    {
        [PieceKind.Queen] = 4,
        [PieceKind.Rook] = 2,
        [PieceKind.Bishop] = 1,
        [PieceKind.Knight] = 1,
        [PieceKind.Pawn] = 0,
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
    private readonly Dictionary<PieceKind, int> timesTaken;
    private readonly Dictionary<Side, int> spent = new() { [Side.White] = 0, [Side.Black] = 0 };
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
        timesTaken = PoolSize.ToDictionary(entry => entry.Key, _ => 0);
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

    public Side? SideToPick { get; private set; }

    public Side SideToMove => board?.SideToMove ?? Side.White;

    public GameEndResult? EndResult => result;

    public bool IsGameOver => result is not null;

    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;

    public IReadOnlyList<string> MoveNotations => notations;

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

    public int SpentBy(Side side) => spent[side];

    public int BudgetLeft(Side side) => Budget - SpentBy(side);

    public TimeSpan TimeBonusOf(Side side) => TimeSpan.FromSeconds(BudgetLeft(side) * SecondsPerUnspentPoint);

    public int PriceOf(PieceKind kind) =>
        BasePrices.TryGetValue(kind, out var price) ? price + (PriceRise[kind] * timesTaken[kind]) : int.MaxValue;

    public IReadOnlyDictionary<Square, PieceKind> PlacementsOf(Side side) => placements[side];

    public bool HasFinishedPlacing(Side side) =>
        placements[side].Count == picks[side].Count + 1 && placements[side].ContainsValue(PieceKind.King);

    public void Pick(Side side, PieceKind kind)
    {
        EnsureNotOver();

        if (Phase != DraftPhase.Drafting)
            throw new InvalidOperationException("The draft is over.");

        if (SideToPick != side)
            throw new InvalidOperationException($"It is not {side}'s pick.");

        if (!CanPick(side, kind))
            throw new InvalidOperationException($"{side} cannot take a {kind} right now.");

        spent[side] += PriceOf(kind);
        pool[kind]--;
        timesTaken[kind]++;
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
        if (!BasePrices.ContainsKey(kind) || pool.GetValueOrDefault(kind) == 0)
            return false;

        if (PriceOf(kind) > BudgetLeft(side))
            return false;

        return kind == PieceKind.Pawn
            ? picks[side].Count(pick => pick == PieceKind.Pawn) < MaxPawns
            : picks[side].Count(pick => pick != PieceKind.Pawn) < MaxOfficers;
    }

    private bool CanPickAnything(Side side) =>
        !passed[side] && BasePrices.Keys.Any(kind => CanPick(side, kind));

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

        if (Remaining(side, kind) == 0)
            throw new InvalidOperationException($"{side} has no {kind} left to place.");

        if (!CanPlace(side, kind, square))
            throw new InvalidOperationException($"{side} cannot put a {kind} on {square}.");

        placements[side][square] = kind;

        if (Sides.All(HasFinishedPlacing))
            StartPlay();
    }

    public bool CanPlace(Side side, PieceKind kind, Square square)
    {
        if (placements[side].ContainsKey(square) || Remaining(side, kind) == 0)
            return false;

        if (kind == PieceKind.Pawn)
            return square.Rank == PawnRank(side);

        if (kind != PieceKind.King)
            return square.Rank == BackRank(side);

        if (square.Rank == BackRank(side))
            return true;

        return square.Rank == PawnRank(side) && FreeSquaresOn(side, PawnRank(side)) > Remaining(side, PieceKind.Pawn);
    }

    public IReadOnlyList<Square> SquaresFor(Side side, PieceKind kind) =>
        [.. AllSquares().Where(square => CanPlace(side, kind, square))];

    private static IEnumerable<Square> AllSquares()
    {
        for (var rank = 0; rank < 8; rank++)
        {
            for (var file = 0; file < 8; file++)
                yield return new Square(file, rank);
        }
    }

    private int FreeSquaresOn(Side side, int rank) =>
        8 - placements[side].Keys.Count(square => square.Rank == rank);

    public void Unplace(Side side, Square square)
    {
        EnsureNotOver();

        if (Phase != DraftPhase.Placing)
            throw new InvalidOperationException("The layout is already locked in.");

        if (!placements[side].Remove(square))
            throw new InvalidOperationException($"{side} has nothing on {square}.");
    }

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

        foreach (var side in Sides)
            Clock.Add(side, TimeBonusOf(side));

        board = built;
        board.SetSideToMove(Side.White);
        Phase = DraftPhase.Playing;

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
