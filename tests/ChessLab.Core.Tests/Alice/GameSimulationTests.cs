using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using Xunit.Sdk;
using GameState = ChessLab.Core.Alice.GameState;

namespace ChessLab.Core.Tests.Alice;

public class GameSimulationTests
{
    [Fact]
    public void ManySimulatedGames_KeepThePiecesOnOneBoardEach_AndFinish()
    {
        var endReasons = new List<GameEndReason>();

        for (var seed = 0; seed < 30; seed++)
        {
            var rng = new Random(seed);
            var room = new Room("SIM", Guid.NewGuid(), GameKind.AliceChess);
            room.ClaimSeat(new SeatId(Side.White, SeatRole.Player), Guid.NewGuid(), "White");
            room.ClaimSeat(new SeatId(Side.Black, SeatRole.Player), Guid.NewGuid(), "Black");

            var session = new AliceChessSession(room);
            session.Start(TimeSpan.FromHours(1), TimeSpan.Zero, DateTimeOffset.UtcNow);
            var game = session.Game!;
            var log = new List<string>();

            for (var ply = 0; ply < GameState.MoveLimit + 10 && !game.IsGameOver; ply++)
            {
                var moves = game.AvailableMoves;
                if (moves.Count == 0)
                    throw new XunitException($"Seed {seed}: no move available but the game is not over.\n{string.Join(' ', log)}");

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
                AssertBoardsAreDisjoint(game, seed, log);
                AssertBothKingsAreStillThere(game, seed, log);
            }

            Assert.True(game.IsGameOver, $"Seed {seed} never finished.\n{string.Join(' ', log)}");
            endReasons.Add(game.EndResult!.Value.Reason);
        }

        Assert.All(endReasons, reason => Assert.Contains(reason,
            (GameEndReason[])[GameEndReason.Checkmate, GameEndReason.Stalemate, GameEndReason.MoveLimit]));
        Assert.Contains(GameEndReason.Checkmate, endReasons);
    }

    private static void AssertBoardsAreDisjoint(GameState game, int seed, IReadOnlyList<string> log)
    {
        for (var file = 0; file < 8; file++)
        {
            for (var rank = 0; rank < 8; rank++)
            {
                var square = new Square(file, rank);
                var onA = game.FenOf(0);
                var onB = game.FenOf(1);
                if (PieceAt(onA, square) is not null && PieceAt(onB, square) is not null)
                    throw new XunitException($"Seed {seed}: {square} is occupied on both boards.\n{string.Join(' ', log)}");
            }
        }
    }

    private static void AssertBothKingsAreStillThere(GameState game, int seed, IReadOnlyList<string> log)
    {
        var symbols = game.FenOf(0).Split(' ')[0] + game.FenOf(1).Split(' ')[0];
        var white = symbols.Count(symbol => symbol == 'K');
        var black = symbols.Count(symbol => symbol == 'k');

        if (white != 1 || black != 1)
            throw new XunitException($"Seed {seed}: expected one king each but found {white}/{black}.\n{string.Join(' ', log)}");
    }

    private static char? PieceAt(string fen, Square square) => FenBoard.PieceAt(fen, square);
}
