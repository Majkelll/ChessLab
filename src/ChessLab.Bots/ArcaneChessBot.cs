using ChessLab.Core.ArcaneChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using GameState = ChessLab.Core.ArcaneChess.GameState;

namespace ChessLab.Bots;

/// <summary>Move selection mirrors <see cref="CardChessBot"/> (Stockfish over the current hand's
/// available moves). Spell casting is a simple, non-exhaustive heuristic — not every spell needs a
/// bot that can pick a good target, so this only ever casts the ones where a reasonable target is
/// cheap to find (heal when hurt, escape an emergency move for free, otherwise occasionally poke at
/// the opponent). It never attempts Swap, Teleport, Execution, Mind Swap, Time Freeze, or Extra
/// Turn — picking a good target for those needs real board evaluation, which isn't worth building
/// for a bot opponent.</summary>
public sealed class ArcaneChessBot(StockfishEngine engine) : IGameBot
{
    private const double CastChance = 0.3;

    private int spellTriedAtMoveCount = -1;

    public async Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var game = ((ArcaneChessSession)session).Game!;

        if (spellTriedAtMoveCount != game.MoveHistory.Count)
        {
            spellTriedAtMoveCount = game.MoveHistory.Count;
            if (ChooseSpell(game, game.SideToMove) is { } cast)
                return GameAction.CastSpell(cast.Spell, cast.Target);
        }

        var move = await ChooseMoveAsync(game, difficulty, ct);
        return GameAction.MovePiece(move.From, move.To, move.PromoteTo);
    }

    public async Task<ChessMove> ChooseMoveAsync(GameState game, BotDifficulty difficulty, CancellationToken ct = default)
    {
        var preset = DifficultyPresets.For(difficulty);
        var moves = game.AvailableMoves;
        var candidates = moves.Select(m => m.ToUci()).Distinct().ToArray();

        var (bestUci, _) = await engine.GoAsync(game.ToFen(), candidates, preset.Elo, preset.MovetimeMs, ct);
        var (from, to, promotion) = UciNotation.ParseUciMove(bestUci);

        return moves.First(m => m.From == from && m.To == to && m.PromoteTo == promotion);
    }

    /// <summary>Returns a spell to cast this turn, or null to skip straight to moving.</summary>
    public (SpellRank Spell, SpellTarget Target)? ChooseSpell(GameState game, Side side)
    {
        var hand = game.SpellHandOf(side);
        var mana = game.ManaOf(side);

        if (game.EmergencyMoveAvailable && Affordable(hand, mana, SpellRank.DeepBreath))
            return (SpellRank.DeepBreath, default);

        if (game.HpOf(side) < ChessLab.Core.CardChess.GameState.StartingHp)
        {
            if (Affordable(hand, mana, SpellRank.Restoration))
                return (SpellRank.Restoration, default);
            if (Affordable(hand, mana, SpellRank.Mend))
                return (SpellRank.Mend, default);
        }

        if (Random.Shared.NextDouble() > CastChance)
            return null;

        var fen = game.ToFen();
        var opponent = side.Opposite();

        foreach (var spell in hand.OrderBy(_ => Random.Shared.Next()))
        {
            if (mana < spell.Cost())
                continue;

            var target = spell switch
            {
                SpellRank.Peek or SpellRank.Jam or SpellRank.Feint or SpellRank.Reshuffle => new SpellTarget(),
                SpellRank.SnapSwap => RandomHandCard(game, side),
                SpellRank.Shield => RandomSquareWhere(fen, c => IsOwn(c, side) && !IsKing(c)) is { } s ? new SpellTarget(Primary: s) : null,
                SpellRank.FreezeSquare => RandomEmptySquare(fen) is { } s ? new SpellTarget(Primary: s) : null,
                SpellRank.PinDown or SpellRank.Disarm =>
                    RandomSquareWhere(fen, c => IsOwn(c, opponent) && !IsKing(c)) is { } s ? new SpellTarget(Primary: s) : null,
                SpellRank.Dispel => game.ActiveEffects.Count > 0
                    ? new SpellTarget(Primary: game.ActiveEffects[Random.Shared.Next(game.ActiveEffects.Count)].Square)
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

    private static SpellTarget? RandomHandCard(GameState game, Side side)
    {
        var hand = game.HandOf(side);
        return hand.Count == 0 ? null : new SpellTarget(HandCard: hand[Random.Shared.Next(hand.Count)]);
    }

    private static Square? RandomEmptySquare(string fen) =>
        PickRandom(AllSquares().Where(sq => FenBoard.PieceAt(fen, sq) is null));

    private static Square? RandomSquareWhere(string fen, Func<char, bool> predicate) =>
        PickRandom(AllSquares().Where(sq => FenBoard.PieceAt(fen, sq) is { } c && predicate(c)));

    private static Square? PickRandom(IEnumerable<Square> squares)
    {
        var candidates = squares.ToList();
        return candidates.Count == 0 ? null : candidates[Random.Shared.Next(candidates.Count)];
    }

    private static IEnumerable<Square> AllSquares()
    {
        for (var file = 0; file < 8; file++)
            for (var rank = 0; rank < 8; rank++)
                yield return new Square(file, rank);
    }
}
