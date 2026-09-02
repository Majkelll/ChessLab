using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;

namespace ChessLab.Core.ArcaneChess;

/// <summary>Card Chess plus a second, spell-card layer on top. Wraps a <see cref="CardChess.GameState"/>
/// by composition — the rank-card hand, HP, and reroll rules are entirely Card Chess's; this type only
/// adds mana, spell hands, and the handful of extra restrictions/mutations spells cause.</summary>
public sealed class GameState
{
    public const int SpellHandSize = 3;
    public const int MaxMana = 3;

    private readonly CardChess.GameState inner;
    private readonly Dictionary<Side, SpellDeck> spellDecks;
    private readonly Dictionary<Side, List<SpellRank>> spellHands;
    private readonly Dictionary<Side, int> mana;
    private readonly Dictionary<Side, bool> spellCastThisTurn;
    private readonly Dictionary<Side, bool> manaGainBlocked;
    private readonly Dictionary<Side, bool> peekActive;
    private readonly Dictionary<Side, bool> deepBreathActive;
    private readonly Dictionary<Side, bool> pendingExtraTurn;
    private readonly List<ArcaneEffect> activeEffects = [];

    public Clock Clock => inner.Clock;
    public IReadOnlyList<ChessMove> MoveHistory => inner.MoveHistory;
    public bool EmergencyMoveAvailable => inner.EmergencyMoveAvailable;
    public bool HandHasNoPlayableCard => inner.HandHasNoPlayableCard;
    public Side SideToMove => inner.SideToMove;
    public GameEndResult? EndResult => inner.EndResult;
    public bool IsGameOver => inner.IsGameOver;

    public int HpOf(Side side) => inner.HpOf(side);
    public IReadOnlyList<CardRank> HandOf(Side side) => inner.HandOf(side);
    public IReadOnlyList<CardRank> PendingRerollOf(Side side) => inner.PendingRerollOf(side);
    public string ToFen() => inner.ToFen();

    public int ManaOf(Side side) => mana[side];
    public IReadOnlyList<SpellRank> SpellHandOf(Side side) => spellHands[side];
    public bool HasCastSpellThisTurn(Side side) => spellCastThisTurn[side];
    public bool IsOpponentHandRevealedTo(Side side) => peekActive[side];
    public IReadOnlyList<ArcaneEffect> ActiveEffects => activeEffects;

    public IReadOnlyList<ChessMove> AvailableMoves => FilterForCurrentMover();

    public GameState(CardChess.GameState inner, SpellDeck whiteSpellDeck, SpellDeck blackSpellDeck)
    {
        this.inner = inner;
        spellDecks = new Dictionary<Side, SpellDeck> { [Side.White] = whiteSpellDeck, [Side.Black] = blackSpellDeck };
        spellHands = new Dictionary<Side, List<SpellRank>>
        {
            [Side.White] = [.. Enumerable.Range(0, SpellHandSize).Select(_ => whiteSpellDeck.Draw())],
            [Side.Black] = [.. Enumerable.Range(0, SpellHandSize).Select(_ => blackSpellDeck.Draw())],
        };
        mana = new Dictionary<Side, int> { [Side.White] = 0, [Side.Black] = 0 };
        spellCastThisTurn = new Dictionary<Side, bool> { [Side.White] = false, [Side.Black] = false };
        manaGainBlocked = new Dictionary<Side, bool> { [Side.White] = false, [Side.Black] = false };
        peekActive = new Dictionary<Side, bool> { [Side.White] = false, [Side.Black] = false };
        deepBreathActive = new Dictionary<Side, bool> { [Side.White] = false, [Side.Black] = false };
        pendingExtraTurn = new Dictionary<Side, bool> { [Side.White] = false, [Side.Black] = false };

        // The side to move first gets their one turn-start mana gain now; the other side gets
        // theirs the first time AdvanceTurnBookkeeping runs for them.
        mana[inner.SideToMove] = 1;
    }

    public void CastSpell(SpellRank spell, SpellTarget target)
    {
        EnsureNotOver();

        var caster = inner.SideToMove;
        var opponent = caster.Opposite();

        if (spellCastThisTurn[caster])
            throw new InvalidOperationException("Already cast a spell this turn.");

        var cost = spell.Cost();
        if (mana[caster] < cost)
            throw new InvalidOperationException($"Not enough mana for {spell.Label()} (needs {cost}, have {mana[caster]}).");

        if (!spellHands[caster].Contains(spell))
            throw new InvalidOperationException($"{spell.Label()} is not in hand.");

        ApplyEffect(spell, caster, opponent, target);

        spellHands[caster].Remove(spell);
        spellHands[caster].Add(spellDecks[caster].Draw());
        mana[caster] -= cost;
        spellCastThisTurn[caster] = true;
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, TimeSpan elapsed)
    {
        EnsureNotOver();

        var mover = inner.SideToMove;

        if (!AvailableMoves.Any(m => m.From == from && m.To == to && m.PromoteTo == promoteTo))
            throw new InvalidOperationException($"{from}-{to} is not a legal move right now.");

        var hpBefore = inner.HpOf(mover);
        var wantsBonusTurn = pendingExtraTurn[mover];

        var move = inner.MakeMove(from, to, promoteTo, elapsed);

        if (deepBreathActive[mover])
        {
            deepBreathActive[mover] = false;
            if (inner.HpOf(mover) < hpBefore)
                inner.AdjustHp(mover, 1);
        }

        activeEffects.RemoveAll(e => e.AffectedSide == mover);

        if (wantsBonusTurn)
        {
            pendingExtraTurn[mover] = false;
            inner.ApplyExternalFenEdit(fen => FenBoard.WithSideToMove(fen, mover));
        }
        else
        {
            AdvanceTurnBookkeeping(inner.SideToMove);
        }

        return move;
    }

    public void SelectCardsForReroll(IReadOnlyList<CardRank> cards) => inner.SelectCardsForReroll(cards);

    public void Resign(Side side) => inner.Resign(side);

    public void DeclareTimeoutIfFlagged() => inner.DeclareTimeoutIfFlagged();

    private void AdvanceTurnBookkeeping(Side newMover)
    {
        spellCastThisTurn[newMover] = false;
        peekActive[newMover] = false;

        if (manaGainBlocked[newMover])
            manaGainBlocked[newMover] = false;
        else
            mana[newMover] = Math.Min(MaxMana, mana[newMover] + 1);
    }

    private IReadOnlyList<ChessMove> FilterForCurrentMover()
    {
        var mover = inner.SideToMove;
        var moves = inner.AvailableMoves.AsEnumerable();

        foreach (var effect in activeEffects.Where(e => e.AffectedSide == mover))
        {
            moves = effect.Kind switch
            {
                ArcaneEffectKind.Shield => moves.Where(m => !(m.To == effect.Square && m.CapturedPiece is not null)),
                ArcaneEffectKind.Freeze => moves.Where(m => m.To != effect.Square),
                ArcaneEffectKind.PinDown => moves.Where(m => m.From != effect.Square),
                ArcaneEffectKind.Disarm => moves.Where(m => !(m.From == effect.Square && m.CapturedPiece is not null)),
                _ => moves,
            };
        }

        return moves.ToArray();
    }

    private void ApplyEffect(SpellRank spell, Side caster, Side opponent, SpellTarget target)
    {
        switch (spell)
        {
            case SpellRank.Shield:
            {
                var sq = RequireSquare(target.Primary);
                RequireOwnOccupied(sq, caster, excludeKing: false);
                activeEffects.Add(new ArcaneEffect(opponent, ArcaneEffectKind.Shield, sq));
                break;
            }
            case SpellRank.FreezeSquare:
            {
                var sq = RequireSquare(target.Primary);
                RequireEmpty(sq);
                activeEffects.Add(new ArcaneEffect(opponent, ArcaneEffectKind.Freeze, sq));
                break;
            }
            case SpellRank.Peek:
                peekActive[caster] = true;
                break;
            case SpellRank.SnapSwap:
            {
                var card = target.HandCard ?? throw new InvalidOperationException("Snap Swap needs a hand card.");
                inner.ReplaceHandCardNow(caster, card);
                break;
            }
            case SpellRank.Feint:
                inner.ClearPendingReroll(opponent);
                break;
            case SpellRank.Jam:
                manaGainBlocked[opponent] = true;
                break;
            case SpellRank.DeepBreath:
                deepBreathActive[caster] = true;
                break;
            case SpellRank.Swap:
            {
                var a = RequireSquare(target.Primary);
                var b = RequireSquare(target.Secondary);
                RequireDistinct(a, b);
                RequireOwnOccupied(a, caster, excludeKing: true);
                RequireOwnOccupied(b, caster, excludeKing: true);
                inner.ApplyExternalFenEdit(fen => FenBoard.SwapPieces(fen, a, b));
                break;
            }
            case SpellRank.Teleport:
            {
                var from = RequireSquare(target.Primary);
                var to = RequireSquare(target.Secondary);
                RequireOwnOccupied(from, caster, excludeKing: true);
                RequireEmpty(to);
                var piece = FenBoard.PieceAt(inner.ToFen(), from)!.Value;
                if (char.ToLowerInvariant(piece) == 'p' && (to.Rank == 0 || to.Rank == 7))
                    throw new InvalidOperationException("A pawn can't teleport onto the back rank.");
                inner.ApplyExternalFenEdit(fen => FenBoard.MovePiece(fen, from, to));
                break;
            }
            case SpellRank.PinDown:
            {
                var sq = RequireSquare(target.Primary);
                RequireOpponentOccupied(sq, caster, excludeKing: true);
                activeEffects.Add(new ArcaneEffect(opponent, ArcaneEffectKind.PinDown, sq));
                break;
            }
            case SpellRank.Disarm:
            {
                var sq = RequireSquare(target.Primary);
                RequireOpponentOccupied(sq, caster, excludeKing: true);
                activeEffects.Add(new ArcaneEffect(opponent, ArcaneEffectKind.Disarm, sq));
                break;
            }
            case SpellRank.Mend:
                inner.AdjustHp(caster, 1);
                break;
            case SpellRank.Dispel:
            {
                var sq = RequireSquare(target.Primary);
                if (activeEffects.RemoveAll(e => e.Square == sq) == 0)
                    throw new InvalidOperationException($"No active effect at {sq}.");
                break;
            }
            case SpellRank.Reshuffle:
            {
                var hand = inner.HandOf(opponent);
                if (hand.Count == 0)
                    throw new InvalidOperationException("Opponent has no cards.");
                inner.ReplaceHandCardNow(opponent, hand[Random.Shared.Next(hand.Count)]);
                break;
            }
            case SpellRank.ExtraTurn:
                pendingExtraTurn[caster] = true;
                break;
            case SpellRank.Execution:
            {
                var sq = RequireSquare(target.Primary);
                RequireOpponentOccupied(sq, caster, excludeKing: false);
                if (char.ToLowerInvariant(FenBoard.PieceAt(inner.ToFen(), sq)!.Value) != 'p')
                    throw new InvalidOperationException($"{sq} is not a pawn.");
                inner.ApplyExternalFenEdit(fen => FenBoard.MovePiece(fen, sq, null));
                break;
            }
            case SpellRank.Restoration:
                inner.AdjustHp(caster, 2);
                break;
            case SpellRank.MindSwap:
            {
                var sq = RequireSquare(target.Primary);
                RequireOwnOccupied(sq, caster, excludeKing: true);
                var kingSquare = FindKing(caster);
                inner.ApplyExternalFenEdit(fen => FenBoard.SwapPieces(fen, kingSquare, sq));
                break;
            }
            case SpellRank.TimeFreeze:
            {
                var a = RequireSquare(target.Primary);
                var b = RequireSquare(target.Secondary);
                RequireDistinct(a, b);
                RequireEmpty(a);
                RequireEmpty(b);
                activeEffects.Add(new ArcaneEffect(opponent, ArcaneEffectKind.Freeze, a));
                activeEffects.Add(new ArcaneEffect(opponent, ArcaneEffectKind.Freeze, b));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(spell));
        }
    }

    private Square FindKing(Side side)
    {
        var fen = inner.ToFen();
        var kingChar = side == Side.White ? 'K' : 'k';
        for (var file = 0; file < 8; file++)
        {
            for (var rank = 0; rank < 8; rank++)
            {
                var square = new Square(file, rank);
                if (FenBoard.PieceAt(fen, square) == kingChar)
                    return square;
            }
        }

        throw new InvalidOperationException($"{side} has no king on the board.");
    }

    private static Square RequireSquare(Square? square) =>
        square ?? throw new InvalidOperationException("This spell needs a target square.");

    private static void RequireDistinct(Square a, Square b)
    {
        if (a == b)
            throw new InvalidOperationException("Pick two different squares.");
    }

    private void RequireEmpty(Square square)
    {
        if (FenBoard.PieceAt(inner.ToFen(), square) is not null)
            throw new InvalidOperationException($"{square} is not empty.");
    }

    private void RequireOwnOccupied(Square square, Side side, bool excludeKing)
    {
        var piece = FenBoard.PieceAt(inner.ToFen(), square)
            ?? throw new InvalidOperationException($"{square} is empty.");

        if ((side == Side.White) != char.IsUpper(piece))
            throw new InvalidOperationException($"{square} is not your piece.");

        if (excludeKing && char.ToLowerInvariant(piece) == 'k')
            throw new InvalidOperationException("The king can't be targeted this way.");
    }

    private void RequireOpponentOccupied(Square square, Side casterSide, bool excludeKing)
    {
        var piece = FenBoard.PieceAt(inner.ToFen(), square)
            ?? throw new InvalidOperationException($"{square} is empty.");

        if ((casterSide == Side.White) == char.IsUpper(piece))
            throw new InvalidOperationException($"{square} is your own piece.");

        if (excludeKing && char.ToLowerInvariant(piece) == 'k')
            throw new InvalidOperationException("The king can't be targeted.");
    }

    private void EnsureNotOver()
    {
        if (IsGameOver)
            throw new InvalidOperationException("The game has already ended.");
    }
}
