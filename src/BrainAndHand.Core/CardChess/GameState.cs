using BrainAndHand.Core.Chess;
using BrainAndHand.Core.HandBrain;

namespace BrainAndHand.Core.CardChess;

/// <summary>
/// Orchestrates a Card Chess game on top of an <see cref="IChessRulesEngine"/>: each side holds a
/// hand of <see cref="HandSize"/> cards, always visible, and on its turn may move any piece of the
/// kind represented by a hand card that currently has a legal move — there's no separate "draw"
/// step, a move is made directly and the server works out which hand card it used. That card is
/// then discarded and replaced with a fresh random one, keeping the hand at a constant size — and
/// every card dealt, whether at the initial deal or a refill, is guaranteed to have a legal move for
/// that side at the moment it's dealt, so a hand is never dealt a card for a piece that's already
/// gone (a captured queen) or that simply can't move yet (the king at the very start of the game) —
/// though a card can still go dead later as the position changes, same as before. If a
/// hand has no playable card while in check, an Emergency Move — any legal check-escaping move, for
/// 1 HP — becomes available instead, and running out of HP when one is needed loses the game
/// outright (rules 7/8). If a hand has no playable card and the side *isn't* in check, that's a gap
/// the written rules don't cover (they only ever describe Emergency Move as a check-response) —
/// rather than soft-locking the game, the same "any legal move" fallback applies, but for free.
/// </summary>
public sealed class GameState
{
    public const int StartingHp = 3;
    public const int HandSize = 5;

    private readonly IChessRulesEngine engine;
    private readonly Dictionary<Side, Deck> decks;
    private readonly Dictionary<Side, List<CardRank>> hands;
    private readonly Dictionary<Side, int> hp;
    private readonly List<ChessMove> moveHistory = [];
    private GameEndResult? forcedResult;

    public Clock Clock { get; }
    public IReadOnlyList<ChessMove> MoveHistory => moveHistory;
    public IReadOnlyList<ChessMove> AvailableMoves { get; private set; } = [];

    /// <summary>True only when the side to move is in check and no hand card can escape it — this
    /// is the one case an Emergency Move actually costs HP (rules 7/8).</summary>
    public bool EmergencyMoveAvailable { get; private set; }

    /// <summary>True whenever no hand card has a legal move at all, whether or not that's from
    /// check — <see cref="AvailableMoves"/> falls back to every legal move either way, but only
    /// <see cref="EmergencyMoveAvailable"/> costs HP.</summary>
    public bool HandHasNoPlayableCard { get; private set; }

    public Side SideToMove => engine.SideToMove;
    public GameEndResult? EndResult => engine.EndResult ?? forcedResult;
    public bool IsGameOver => EndResult is not null;

    public int HpOf(Side side) => hp[side];
    public IReadOnlyList<CardRank> HandOf(Side side) => hands[side];

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

        RefreshAvailableMoves();
    }

    private IReadOnlyList<ChessMove> LegalMovesFor(CardRank card) => engine.LegalMoves(card.ToPieceKind());

    /// <summary>Draws a card guaranteed to have a legal move for <paramref name="side"/> right now —
    /// so a hand is never dealt a card for a piece that's already gone (a captured queen) or that
    /// simply can't move yet (the king at the very start of the game).</summary>
    private CardRank DrawPlayable(Side side, Deck deck) => deck.Draw(card => engine.HasLegalMove(side, card.ToPieceKind()));

    /// <summary>Recomputes what the side to move is currently allowed to play — every legal move
    /// reachable through one of its hand cards, or the full-board fallback if none of them have one.</summary>
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

    /// <summary>Call periodically from outside (e.g. a server-side timer) to end the game if a side's clock hit zero.</summary>
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
