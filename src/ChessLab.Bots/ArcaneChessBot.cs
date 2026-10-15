using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public sealed class ArcaneChessBot(IChessEngine engine, int? seed = null) : IGameBot
{
    private const double CastChance = 0.3;

    private readonly Random random = BotRandom.For(seed);

    public async Task<GameAction> ChooseActionAsync(GameStateEnvelopeDto state, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var arcane = state.Arcane ?? throw new InvalidOperationException("The state has no Arcane Chess section.");

        if (!arcane.SideToMoveHasCast && ChooseSpell(state, seat.Side) is { } cast)
            return GameAction.CastSpell(cast.Spell, cast.Target);

        var move = await EngineMoves.BestAsync(engine, state.PositionText, state.AvailableMoves,
            DifficultyPresets.For(difficulty), ct);
        return GameBots.MoveAction(move);
    }

    public (SpellRank Spell, SpellTarget Target)? ChooseSpell(GameStateEnvelopeDto state, Side side)
    {
        var arcane = state.Arcane!;
        var cards = state.CardChess!;
        var hand = side == Side.White ? arcane.WhiteSpellHand : arcane.BlackSpellHand;
        var mana = side == Side.White ? arcane.WhiteMana : arcane.BlackMana;
        var hp = side == Side.White ? cards.WhiteHp : cards.BlackHp;

        if (cards.EmergencyMoveAvailable && Affordable(hand, mana, SpellRank.DeepBreath))
            return (SpellRank.DeepBreath, default);

        if (hp < ChessLab.Core.CardChess.GameState.StartingHp)
        {
            if (Affordable(hand, mana, SpellRank.Restoration))
                return (SpellRank.Restoration, default);
            if (Affordable(hand, mana, SpellRank.Mend))
                return (SpellRank.Mend, default);
        }

        if (random.NextDouble() > CastChance)
            return null;

        var fen = state.PositionText;
        var opponent = side.Opposite();
        var rankHand = side == Side.White ? cards.WhiteHand : cards.BlackHand;

        foreach (var spell in hand.OrderBy(_ => random.Next()))
        {
            if (mana < spell.Cost())
                continue;

            var target = spell switch
            {
                SpellRank.Peek or SpellRank.Jam or SpellRank.Feint or SpellRank.Reshuffle => new SpellTarget(),
                SpellRank.SnapSwap => RandomHandCard(rankHand),
                SpellRank.Shield => RandomSquareWhere(fen, c => IsOwn(c, side) && !IsKing(c)) is { } s ? new SpellTarget(Primary: s) : null,
                SpellRank.FreezeSquare => RandomEmptySquare(fen) is { } s ? new SpellTarget(Primary: s) : null,
                SpellRank.PinDown or SpellRank.Disarm =>
                    RandomSquareWhere(fen, c => IsOwn(c, opponent) && !IsKing(c)) is { } s ? new SpellTarget(Primary: s) : null,
                SpellRank.Dispel => arcane.ActiveEffects.Count > 0
                    ? new SpellTarget(Primary: arcane.ActiveEffects[random.Next(arcane.ActiveEffects.Count)].Square)
                    : null,
                _ => null,
            };

            if (target is { } t)
                return (spell, t);
        }

        return null;
    }

    private static bool Affordable(IReadOnlyList<SpellRank> hand, int mana, SpellRank spell) =>
        hand.Contains(spell) && mana >= spell.Cost();

    private static bool IsOwn(char piece, Side side) => (side == Side.White) == char.IsUpper(piece);

    private static bool IsKing(char piece) => char.ToLowerInvariant(piece) == 'k';

    private SpellTarget? RandomHandCard(IReadOnlyList<CardRank> hand) =>
        hand.Count == 0 ? null : new SpellTarget(HandCard: hand[random.Next(hand.Count)]);

    private Square? RandomEmptySquare(string fen) =>
        PickRandom(AllSquares().Where(sq => FenBoard.PieceAt(fen, sq) is null));

    private Square? RandomSquareWhere(string fen, Func<char, bool> predicate) =>
        PickRandom(AllSquares().Where(sq => FenBoard.PieceAt(fen, sq) is { } c && predicate(c)));

    private Square? PickRandom(IEnumerable<Square> squares)
    {
        var candidates = squares.ToList();
        return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
    }

    private static IEnumerable<Square> AllSquares()
    {
        for (var file = 0; file < 8; file++)
            for (var rank = 0; rank < 8; rank++)
                yield return new Square(file, rank);
    }
}
