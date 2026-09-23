using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using ArcaneGameState = ChessLab.Core.ArcaneChess.GameState;

namespace ChessLab.Core.Tests.ArcaneChess;

public class GameSimulationTests
{
    [Fact]
    public void ManySimulatedGames_EveryTurnEverySpell_NeverThrowsOutsideInvalidOperationException()
    {
        for (var gameSeed = 0; gameSeed < 25; gameSeed++)
        {
            var rng = new Random(gameSeed);
            var room = new Room("SIM", Guid.NewGuid(), GameKind.ArcaneChess);
            room.ClaimSeat(new SeatId(Side.White, SeatRole.Player), Guid.NewGuid(), "White");
            room.ClaimSeat(new SeatId(Side.Black, SeatRole.Player), Guid.NewGuid(), "Black");
            var session = new ArcaneChessSession(room);
            session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, DateTimeOffset.UtcNow, rng);

            var log = new List<string>();

            for (var turn = 0; turn < 300 && !session.Game!.IsGameOver; turn++)
            {
                var game = session.Game!;
                var side = game.SideToMove;

                foreach (var spell in game.SpellHandOf(side).ToArray())
                {
                    if (game.HasCastSpellThisTurn(side))
                        break;
                    if (game.ManaOf(side) < spell.Cost())
                        continue;

                    var target = ChooseTarget(game, side, spell, rng);
                    if (target is null && RequiresTarget(spell))
                        continue;

                    try
                    {
                        var fenBefore = game.ToFen();
                        session.CastSpell(spell, target ?? default);
                        var fenAfter = game.ToFen();
                        log.Add($"turn {turn} {side} cast {spell} target {target} fenBefore={fenBefore} fenAfter={fenAfter}");
                        AssertExactlyTwoKings(fenAfter, gameSeed, turn, $"after {side} cast {spell} targeting {target}", log);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                    catch (Exception ex)
                    {
                        throw new Xunit.Sdk.XunitException(
                            $"Seed {gameSeed}, turn {turn}, {side} cast {spell} targeting {target}: {ex}\n\nLog:\n{string.Join('\n', log)}");
                    }
                }

                if (game.IsGameOver)
                    break;

                IReadOnlyList<ChessMove> moves;
                try
                {
                    moves = game.AvailableMoves;
                }
                catch (Exception ex)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Seed {gameSeed}, turn {turn}: reading AvailableMoves for {side} threw: {ex}\n\nLog:\n{string.Join('\n', log)}");
                }

                if (moves.Count == 0)
                    throw new Xunit.Sdk.XunitException(
                        $"Seed {gameSeed}, turn {turn}: {side} has no available moves but the game isn't over.\n\nLog:\n{string.Join('\n', log)}");

                var move = moves[rng.Next(moves.Count)];
                try
                {
                    session.MakeMove(move.From, move.To, move.PromoteTo, DateTimeOffset.UtcNow);
                    AssertExactlyTwoKings(game.ToFen(), gameSeed, turn, $"after {side} moved {move.From}-{move.To}", log);
                }
                catch (Exception ex)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Seed {gameSeed}, turn {turn}: {side} move {move.From}-{move.To} threw: {ex}\n\nLog:\n{string.Join('\n', log)}");
                }

                log.Add($"turn {turn} {side} moved {move.From}-{move.To}");
            }
        }
    }

    private static void AssertExactlyTwoKings(string fen, int gameSeed, int turn, string when, List<string> log)
    {
        var placement = fen.Split(' ')[0];
        var whiteKings = placement.Count(c => c == 'K');
        var blackKings = placement.Count(c => c == 'k');
        if (whiteKings != 1 || blackKings != 1)
        {
            throw new Xunit.Sdk.XunitException(
                $"Seed {gameSeed}, turn {turn}: {when} left {whiteKings} white king(s) and {blackKings} black king(s) — fen={fen}\n\nLog:\n{string.Join('\n', log)}");
        }
    }

    private static bool RequiresTarget(SpellRank spell) => spell switch
    {
        SpellRank.Peek or SpellRank.Feint or SpellRank.Jam or SpellRank.DeepBreath
            or SpellRank.Mend or SpellRank.Reshuffle or SpellRank.ExtraTurn or SpellRank.Restoration => false,
        _ => true,
    };

    private static SpellTarget? ChooseTarget(ArcaneGameState game, Side side, SpellRank spell, Random rng)
    {
        var fen = game.ToFen();
        var opponent = side == Side.White ? Side.Black : Side.White;

        return spell switch
        {
            SpellRank.Peek or SpellRank.Feint or SpellRank.Jam or SpellRank.DeepBreath
                or SpellRank.Mend or SpellRank.Reshuffle or SpellRank.ExtraTurn or SpellRank.Restoration => new SpellTarget(),
            SpellRank.SnapSwap => game.HandOf(side).Count > 0
                ? new SpellTarget(HandCard: game.HandOf(side)[rng.Next(game.HandOf(side).Count)])
                : null,
            SpellRank.Shield or SpellRank.MindSwap =>
                RandomSquare(fen, rng, c => IsOwn(c, side) && !IsKing(c)) is { } s ? new SpellTarget(Primary: s) : null,
            SpellRank.FreezeSquare =>
                RandomSquare(fen, rng, c => c is null) is { } s ? new SpellTarget(Primary: s) : null,
            SpellRank.PinDown or SpellRank.Disarm =>
                RandomSquare(fen, rng, c => IsOwn(c, opponent) && !IsKing(c)) is { } s ? new SpellTarget(Primary: s) : null,
            SpellRank.Execution =>
                RandomSquare(fen, rng, c => IsOwn(c, opponent) && c is 'p' or 'P') is { } s ? new SpellTarget(Primary: s) : null,
            SpellRank.Dispel => game.ActiveEffects.Count > 0
                ? new SpellTarget(Primary: game.ActiveEffects[rng.Next(game.ActiveEffects.Count)].Square)
                : null,
            SpellRank.Swap => TwoSquares(fen, rng, c => IsOwn(c, side) && !IsKing(c), c => IsOwn(c, side) && !IsKing(c)),
            SpellRank.Teleport => TwoSquares(fen, rng, c => IsOwn(c, side) && !IsKing(c), c => c is null),
            SpellRank.TimeFreeze => TwoSquares(fen, rng, c => c is null, c => c is null),
            _ => null,
        };
    }

    private static SpellTarget? TwoSquares(string fen, Random rng, Func<char?, bool> firstPredicate, Func<char?, bool> secondPredicate)
    {
        var first = RandomSquare(fen, rng, firstPredicate);
        if (first is null)
            return null;

        var secondCandidates = AllSquares()
            .Where(sq => sq != first.Value && secondPredicate(FenBoard.PieceAt(fen, sq)))
            .ToList();
        if (secondCandidates.Count == 0)
            return null;

        return new SpellTarget(Primary: first, Secondary: secondCandidates[rng.Next(secondCandidates.Count)]);
    }

    private static bool IsOwn(char? piece, Side side) => piece is { } p && (side == Side.White) == char.IsUpper(p);

    private static bool IsKing(char? piece) => piece is { } p && char.ToLowerInvariant(p) == 'k';

    private static Square? RandomSquare(string fen, Random rng, Func<char?, bool> predicate)
    {
        var candidates = AllSquares().Where(sq => predicate(FenBoard.PieceAt(fen, sq))).ToList();
        return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
    }

    private static IEnumerable<Square> AllSquares()
    {
        for (var file = 0; file < 8; file++)
            for (var rank = 0; rank < 8; rank++)
                yield return new Square(file, rank);
    }
}
