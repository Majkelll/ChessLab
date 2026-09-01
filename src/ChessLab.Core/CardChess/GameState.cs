using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;

namespace ChessLab.Core.CardChess;

public sealed class GameState
{
    public const int StartingHp = 3;
    public const int HandSize = 5;
    public const int MaxRerollSelection = 2;

    private readonly IChessRulesEngine engine;
    private readonly Dictionary<Side, Deck> decks;
    private readonly Dictionary<Side, List<CardRank>> hands;
    private readonly Dictionary<Side, int> hp;
    private readonly Dictionary<Side, List<CardRank>> pendingRerolls;
    private readonly List<ChessMove> moveHistory = [];
    private GameEndResult? forcedResult;

    public Clock Clock { get; }
    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;
    public IReadOnlyList<ChessMove> AvailableMoves { get; private set; } = [];

    public bool EmergencyMoveAvailable { get; private set; }

    public bool HandHasNoPlayableCard { get; private set; }

    public Side SideToMove => engine.SideToMove;
    public GameEndResult? EndResult => engine.EndResult ?? forcedResult;
    public bool IsGameOver => EndResult is not null;

    public int HpOf(Side side) => hp[side];
    public IReadOnlyList<CardRank> HandOf(Side side) => hands[side];

    public IReadOnlyList<CardRank> PendingRerollOf(Side side) => pendingRerolls[side];

    public string ToFen() => engine.ToFen();

    public GameState(IChessRulesEngine engine, Clock clock, Deck whiteDeck, Deck blackDeck)
    {
        this.engine = engine;
        Clock = clock;
        decks = new Dictionary<Side, Deck> { [Side.White] = whiteDeck, [Side.Black] = blackDeck };
        hp = new Dictionary<Side, int> { [Side.White] = StartingHp, [Side.Black] = StartingHp };
        hands = new Dictionary<Side, List<CardRank>>
        {
            [Side.White] = [.. Enumerable.Range(0, HandSize).Select(_ => DrawPlayable(Side.White, whiteDeck))],
            [Side.Black] = [.. Enumerable.Range(0, HandSize).Select(_ => DrawPlayable(Side.Black, blackDeck))],
        };
        pendingRerolls = new Dictionary<Side, List<CardRank>> { [Side.White] = [], [Side.Black] = [] };

        RefreshAvailableMoves();
    }

    private IReadOnlyList<ChessMove> LegalMovesFor(CardRank card) => engine.LegalMoves(card.ToPieceKind());

    private CardRank DrawPlayable(Side side, Deck deck) => deck.Draw(card => engine.HasLegalMove(side, card.ToPieceKind()));

    public void SelectCardsForReroll(IReadOnlyList<CardRank> cards)
    {
        EnsureNotOver();

        var side = SideToMove;

        if (cards.Count > MaxRerollSelection)
            throw new ArgumentException($"Can only mark up to {MaxRerollSelection} cards for reroll.", nameof(cards));

        if (cards.Distinct().Count() != cards.Count)
            throw new ArgumentException("Cannot mark the same card twice.", nameof(cards));

        if (cards.Any(card => !hands[side].Contains(card)))
            throw new ArgumentException("Can only mark cards that are currently in hand.", nameof(cards));

        pendingRerolls[side] = [.. cards];
    }

    private void ApplyPendingReroll(Side side)
    {
        var pending = pendingRerolls[side];
        if (pending.Count == 0)
            return;

        foreach (var card in pending)
        {
            var index = hands[side].IndexOf(card);
            if (index >= 0)
                hands[side][index] = DrawPlayable(side, decks[side]);
        }

        pending.Clear();
    }

    private void RefreshAvailableMoves()
    {
        EmergencyMoveAvailable = false;
        HandHasNoPlayableCard = false;

        if (IsGameOver)
        {
            AvailableMoves = [];
            return;
        }

        var mover = SideToMove;
        ApplyPendingReroll(mover);

        var moves = hands[mover].SelectMany(LegalMovesFor).Distinct().ToArray();

        if (moves.Length > 0)
        {
            AvailableMoves = moves;
            return;
        }

        HandHasNoPlayableCard = true;

        if (engine.IsInCheck(mover))
        {
            if (hp[mover] <= 0)
            {
                forcedResult = new GameEndResult(GameEndReason.HpDepleted, Opponent(mover));
                AvailableMoves = [];
                return;
            }

            EmergencyMoveAvailable = true;
        }

        AvailableMoves = engine.LegalMoves();
    }

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
        var usedCardIndex = hands[mover].FindIndex(card =>
            LegalMovesFor(card).Any(m => m.From == from && m.To == to && m.PromoteTo == promoteTo));
        var costsHp = usedCardIndex < 0 && EmergencyMoveAvailable;

        engine.ApplyMove(applied);
        moveHistory.Add(applied);

        if (usedCardIndex >= 0)
        {
            hands[mover].RemoveAt(usedCardIndex);
            hands[mover].Add(DrawPlayable(mover, decks[mover]));
        }
        else if (costsHp)
        {
            hp[mover]--;
        }

        Clock.Deduct(mover, elapsed);
        if (!engine.EndResult.HasValue)
            Clock.ApplyIncrement(mover);

        RefreshAvailableMoves();

        return applied;
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

    private static Side Opponent(Side side) => side == Side.White ? Side.Black : Side.White;

    private void EnsureNotOver()
    {
        if (IsGameOver)
            throw new InvalidOperationException("The game has already ended.");
    }
}
