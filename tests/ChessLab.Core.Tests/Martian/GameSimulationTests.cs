using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using Xunit.Sdk;
using GameState = ChessLab.Core.Martian.GameState;

namespace ChessLab.Core.Tests.Martian;

public class GameSimulationTests
{
    private const int StartingPoints = 2 * ((3 * 3) + (3 * 2) + (3 * 1));

    [Fact]
    public void ManySimulatedGames_NeverLoseOrInventPoints_AndFinish()
    {
        var endReasons = new List<GameEndReason>();

        for (var seed = 0; seed < 30; seed++)
        {
            var rng = new Random(seed);
            var room = new Room("SIM", Guid.NewGuid(), GameKind.MartianChess);
            room.ClaimSeat(new SeatId(Side.White, SeatRole.Player), Guid.NewGuid(), "White");
            room.ClaimSeat(new SeatId(Side.Black, SeatRole.Player), Guid.NewGuid(), "Black");

            var session = new MartianChessSession(room);
            session.Start(TimeSpan.FromHours(1), TimeSpan.Zero, DateTimeOffset.UtcNow);
            var game = session.Game!;
            var log = new List<string>();

            for (var ply = 0; ply < GameState.MoveLimit + 10 && !game.IsGameOver; ply++)
            {
                var moves = game.AvailableMoves;
                if (moves.Count == 0)
                    throw new XunitException($"Seed {seed}: no legal move but the game is not over.\n{string.Join(' ', log)}");

                var move = moves[rng.Next(moves.Count)];

                try
                {
                    session.MakeMove(move.From, move.To, DateTimeOffset.UtcNow);
                }
                catch (InvalidOperationException ex)
                {
                    throw new XunitException($"Seed {seed}, ply {ply}: {ex.Message}\n{string.Join(' ', log)}");
                }

                log.Add(game.MoveNotations[^1]);
                AssertPointsAreConserved(game, seed, log);
            }

            Assert.True(game.IsGameOver, $"Seed {seed} never finished.\n{string.Join(' ', log)}");
            endReasons.Add(game.EndResult!.Value.Reason);
        }

        Assert.All(endReasons, reason => Assert.Contains(reason,
            (GameEndReason[])[GameEndReason.BoardHalfEmptied, GameEndReason.Stalemate, GameEndReason.MoveLimit]));
        Assert.Contains(GameEndReason.BoardHalfEmptied, endReasons);
    }

    private static void AssertPointsAreConserved(GameState game, int seed, IReadOnlyList<string> log)
    {
        var onBoard = 0;
        for (var file = 0; file < GameState.Files; file++)
        {
            for (var rank = 0; rank < GameState.Ranks; rank++)
                onBoard += game.PyramidAt(new Square(file, rank));
        }

        var total = onBoard + game.ScoreOf(Side.White) + game.ScoreOf(Side.Black);
        if (total != StartingPoints)
            throw new XunitException($"Seed {seed}: {total} points in play, expected {StartingPoints}.\n{string.Join(' ', log)}");
    }
}
