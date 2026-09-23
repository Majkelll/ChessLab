using ChessLab.Core.Chess;
using ChessLab.Core.Games;

namespace ChessLab.Core.CardChess;

public sealed class GameState : IGameEngineState
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

    public IReadOnlyList<string> MoveNotations => [.. MoveHistory.Select(move => move.San)];

    public string PositionText => ToFen();

    public IReadOnlyList<Side> SidesOnTheClock => [SideToMove];

    public GameState(IChessRulesEngine engine, Clock clock, Deck whiteDeck, Deck blackDeck)
    {
        this.engine = engine;
        Clock = clock;
        decks = new Dictionary<Side, Deck> { [Side.White] = whiteDeck, [Side.Black] = blackDeck };
        hp = new Dictionary<Side, int> { [Side.White] = StartingHp, [Side.Black] = StartingHp };
        hands = new Dictionary<Side, List<CardRank>>
        {
            [Side.White] = DealHand(Side.White, whiteDeck),
            [Side.Black] = DealHand(Side.Black, blackDeck),
        };
        pendingRerolls = new Dictionary<Side, List<CardRank>> { [Side.White] = [], [Side.Black] = [] };

        RefreshAvailableMoves();
    }

    private IReadOnlyList<ChessMove> LegalMovesFor(CardRank card) => engine.LegalMoves(card.ToPieceKind());

    private List<CardRank> DealHand(Side side, Deck deck)
    {
        var hand = new List<CardRank>();
        for (var i = 0; i < HandSize; i++)
            hand.Add(DrawPlayable(side, deck, hand));

        return hand;
    }

    private CardRank DrawPlayable(Side side, Deck deck, IReadOnlyCollection<CardRank> currentHand) =>
        deck.Draw(card => engine.HasLegalMove(side, card.ToPieceKind()) && !currentHand.Contains(card));

    private CardRank DrawRerollReplacement(Side side, CardRank rerolled)
    {
        var first = DrawPlayable(side, decks[side], hands[side]);
        if (first.ToPieceKind() != rerolled.ToPieceKind())
            return first;

        var second = DrawPlayable(side, decks[side], hands[side]);
        return second.ToPieceKind() != rerolled.ToPieceKind() ? second : first;
    }

    private void ReplaceCardsOfLostPieces(Side side)
    {
        var fen = engine.ToFen();
        var hand = hands[side];
        for (var i = 0; i < hand.Count; i++)
        {
            var card = hand[i];
            if (FenBoard.HasPiece(fen, side, card.ToPieceKind()))
                continue;

            hand[i] = DrawPlayable(side, decks[side], hand);
            decks[side].Discard(card);
            pendingRerolls[side].Remove(card);
        }
    }

    public void AdjustHp(Side side, int delta) => hp[side] = Math.Clamp(hp[side] + delta, 0, StartingHp);

    public void ClearPendingReroll(Side side) => pendingRerolls[side].Clear();

    public void ReplaceHandCardNow(Side side, CardRank card)
    {
        var index = hands[side].IndexOf(card);
        if (index < 0)
            throw new ArgumentException($"{card} is not currently in {side}'s hand.", nameof(card));

        hands[side][index] = DrawRerollReplacement(side, card);
        decks[side].Discard(card);
        pendingRerolls[side].Remove(card);
        if (side == SideToMove)
            RefreshAvailableMoves();
    }

    public void ApplyExternalFenEdit(Func<string, string> edit)
    {
        var previousFen = engine.ToFen();
        engine.LoadPosition(edit(previousFen));

        if (engine.IsInCheck(engine.SideToMove))
        {
            engine.LoadPosition(previousFen);
            throw new InvalidOperationException("This would leave your own king in check.");
        }

        RefreshAvailableMoves();
    }

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
        ComputeAvailableMoves();
    }

    private IEnumerable<CardRank> PlayableHandOf(Side side) => hands[side].Except(pendingRerolls[side]);

    private void ApplyPendingReroll(Side side)
    {
        var pending = pendingRerolls[side];
        if (pending.Count == 0)
            return;

        foreach (var card in pending)
        {
            var index = hands[side].IndexOf(card);
            if (index >= 0)
            {
                hands[side][index] = DrawRerollReplacement(side, card);
                decks[side].Discard(card);
            }
        }

        pending.Clear();
    }

    private void RefreshAvailableMoves()
    {
        if (!IsGameOver)
        {
            ApplyPendingReroll(SideToMove);
            ReplaceCardsOfLostPieces(SideToMove);
        }

        ComputeAvailableMoves();
    }

    private void ComputeAvailableMoves()
    {
        EmergencyMoveAvailable = false;
        HandHasNoPlayableCard = false;

        if (IsGameOver)
        {
            AvailableMoves = [];
            return;
        }

        var mover = SideToMove;

        var allLegal = engine.LegalMoves();

        var moves = PlayableHandOf(mover).SelectMany(LegalMovesFor).Distinct()
            .Where(m => m.CapturedPiece != PieceKind.King).ToArray();

        if (moves.Length > 0)
        {
            AvailableMoves = moves;
            return;
        }

        if (pendingRerolls[mover].Any(card => LegalMovesFor(card).Any(m => m.CapturedPiece != PieceKind.King)))
        {
            AvailableMoves = [];
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

        var nonKingCaptureMoves = allLegal.Where(m => m.CapturedPiece != PieceKind.King).ToArray();

        if (nonKingCaptureMoves.Length == 0 && allLegal.Count > 0)
        {
            forcedResult = new GameEndResult(GameEndReason.Checkmate, mover);
            AvailableMoves = [];
            return;
        }

        AvailableMoves = nonKingCaptureMoves;
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
        var usedCardIndex = hands[mover].FindIndex(card => !pendingRerolls[mover].Contains(card) &&
            LegalMovesFor(card).Any(m => m.From == from && m.To == to && m.PromoteTo == promoteTo));
        var costsHp = usedCardIndex < 0 && EmergencyMoveAvailable;

        engine.ApplyMove(applied);
        moveHistory.Add(applied);

        if (usedCardIndex >= 0)
        {
            var usedCard = hands[mover][usedCardIndex];
            hands[mover].RemoveAt(usedCardIndex);
            hands[mover].Add(DrawPlayable(mover, decks[mover], hands[mover]));
            decks[mover].Discard(usedCard);
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
