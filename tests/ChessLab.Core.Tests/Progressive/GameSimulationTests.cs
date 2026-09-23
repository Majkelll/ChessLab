using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using Xunit.Sdk;

namespace ChessLab.Core.Tests.Progressive;

public class GameSimulationTests
{
    [Fact]
    public void ManySimulatedGames_PlayEverySeriesToItsEnd_AndFinish()
    {
        var endReasons = new List<GameEndReason>();

        for (var seed = 0; seed < 30; seed++)
        {
            var rng = new Random(seed);
            var room = new Room("SIM", Guid.NewGuid(), GameKind.ProgressiveChess);
            room.ClaimSeat(new SeatId(Side.White, SeatRole.Player), Guid.NewGuid(), "White");
            room.ClaimSeat(new SeatId(Side.Black, SeatRole.Player), Guid.NewGuid(), "Black");

            var session = new ProgressiveChessSession(room);
            session.Start(TimeSpan.FromHours(1), TimeSpan.Zero, DateTimeOffset.UtcNow);
            var game = session.Game!;
            var log = new List<string>();

            for (var ply = 0; ply < 4000 && !game.IsGameOver; ply++)
            {
                var seriesBefore = game.SeriesNumber;
                var playedBefore = game.MovesPlayedInSeries;
                var mover = game.SideToMove;
                var moves = game.AvailableMoves;

                if (moves.Count == 0)
                    throw new XunitException($"Seed {seed}: no legal move but the game is not over.\n{string.Join(' ', log)}");

                var move = moves[rng.Next(moves.Count)];

                try
                {
                    session.MakeMove(move.From, move.To, move.PromoteTo, DateTimeOffset.UtcNow);
                }
                catch (InvalidOperationException ex)
                {
                    throw new XunitException($"Seed {seed}, ply {ply}: {ex.Message}\n{string.Join(' ', log)}");
                }

                log.Add(game.MoveNotations[^1]);

                var seriesEnded = game.SeriesNumber != seriesBefore;
                if (seriesEnded)
                {
                    var wasLastAllowedMove = playedBefore + 1 >= seriesBefore;
                    if (!wasLastAllowedMove && !move.IsCheck && !game.IsGameOver && game.SideToMove == mover)
                        throw new XunitException($"Seed {seed}, ply {ply}: series ended early without a check.\n{string.Join(' ', log)}");
                }
                else
                {
                    Assert.Equal(mover, game.SideToMove);
                    Assert.False(move.IsCheck, $"Seed {seed}: a checking move must end the series.");
                }
            }

            Assert.True(game.IsGameOver, $"Seed {seed} never finished.\n{string.Join(' ', log)}");
            Assert.Equal(2, game.PositionText.Split(' ')[0].Count(symbol => symbol is 'K' or 'k'));
            endReasons.Add(game.EndResult!.Value.Reason);
        }

        Assert.Contains(GameEndReason.Checkmate, endReasons);
    }
}
