using ChessLab.Core.Bidding;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using Xunit.Sdk;

namespace ChessLab.Core.Tests.Bidding;

public class GameSimulationTests
{
    [Fact]
    public void ManySimulatedGames_BidAndPlayToAFinish_WithoutBreakingTheirOwnRules()
    {
        var endReasons = new List<GameEndReason>();

        for (var seed = 0; seed < 50; seed++)
        {
            var rng = new Random(seed);
            var room = new Room("SIM", Guid.NewGuid(), GameKind.BiddingChess);
            room.ClaimSeat(new SeatId(Side.White, SeatRole.Player), Guid.NewGuid(), "White");
            room.ClaimSeat(new SeatId(Side.Black, SeatRole.Player), Guid.NewGuid(), "Black");

            var session = new BiddingChessSession(room);
            session.Start(TimeSpan.FromHours(1), TimeSpan.Zero, DateTimeOffset.UtcNow);
            var game = session.Game!;
            var log = new List<string>();

            for (var turn = 0; turn < 2 * GameState.MoveLimit + 10 && !game.IsGameOver; turn++)
            {
                try
                {
                    if (game.Phase == BiddingPhase.Bidding)
                    {
                        foreach (var side in (Side[])[Side.White, Side.Black])
                        {
                            var bid = rng.Next(0, Math.Min(game.ChipsOf(side), 25) + 1);
                            session.SubmitBid(side, bid, DateTimeOffset.UtcNow);
                            log.Add($"{side} bids {bid}");
                        }

                        Assert.Equal(2 * GameState.StartingChips,
                            game.ChipsOf(Side.White) + game.ChipsOf(Side.Black));
                        continue;
                    }

                    var moves = game.AvailableMoves;
                    if (moves.Count == 0)
                        throw new XunitException($"Seed {seed}: the side that won the bid has no move.\n{string.Join('\n', log)}");

                    var move = moves[rng.Next(moves.Count)];
                    session.MakeMove(move.From, move.To, move.PromoteTo, DateTimeOffset.UtcNow);
                    log.Add($"{game.MoveNotations[^1]}");
                }
                catch (InvalidOperationException ex)
                {
                    throw new XunitException($"Seed {seed}, turn {turn}: {ex.Message}\n{string.Join('\n', log)}");
                }

                AssertKingCount(game, log, seed);
            }

            Assert.True(game.IsGameOver, $"Seed {seed} never finished.\n{string.Join('\n', log)}");
            endReasons.Add(game.EndResult!.Value.Reason);
        }

        Assert.All(endReasons, reason =>
            Assert.Contains(reason, (GameEndReason[])[GameEndReason.KingCaptured, GameEndReason.FiftyMoveRule, GameEndReason.Stalemate, GameEndReason.MoveLimit]));
        Assert.Contains(GameEndReason.KingCaptured, endReasons);
    }

    private static void AssertKingCount(GameState game, IReadOnlyList<string> log, int seed)
    {
        var placement = game.PositionText.Split(' ')[0];
        var kings = placement.Count(symbol => symbol is 'K' or 'k');
        var expected = game.IsGameOver && game.EndResult!.Value.Reason == GameEndReason.KingCaptured ? 1 : 2;

        if (kings != expected)
            throw new XunitException($"Seed {seed}: expected {expected} kings but found {kings} in {game.PositionText}.\n{string.Join('\n', log)}");
    }
}
