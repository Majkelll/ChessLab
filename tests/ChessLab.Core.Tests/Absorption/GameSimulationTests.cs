using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using Xunit.Sdk;
using GameState = ChessLab.Core.Absorption.GameState;

namespace ChessLab.Core.Tests.Absorption;

public class GameSimulationTests
{
    [Fact]
    public void ManySimulatedGames_OnlyEverGrowAPiecesPowers_AndFinish()
    {
        var endReasons = new List<GameEndReason>();

        for (var seed = 0; seed < 30; seed++)
        {
            var rng = new Random(seed);
            var room = new Room("SIM", Guid.NewGuid(), GameKind.AbsorptionChess);
            room.ClaimSeat(new SeatId(Side.White, SeatRole.Player), Guid.NewGuid(), "White");
            room.ClaimSeat(new SeatId(Side.Black, SeatRole.Player), Guid.NewGuid(), "Black");

            var session = new AbsorptionChessSession(room);
            session.Start(TimeSpan.FromHours(1), TimeSpan.Zero, DateTimeOffset.UtcNow);
            var game = session.Game!;
            var log = new List<string>();

            for (var ply = 0; ply < GameState.MoveLimit + 10 && !game.IsGameOver; ply++)
            {
                var moves = game.AvailableMoves;
                if (moves.Count == 0)
                    throw new XunitException($"Seed {seed}: no legal move but the game is not over.\n{string.Join(' ', log)}");

                var move = moves[rng.Next(moves.Count)];
                var powersBefore = game.PowersAt(move.From);

                try
                {
                    session.MakeMove(move.From, move.To, move.PromoteTo, DateTimeOffset.UtcNow);
                }
                catch (InvalidOperationException ex)
                {
                    throw new XunitException($"Seed {seed}, ply {ply}: {ex.Message}\n{string.Join(' ', log)}");
                }

                log.Add(game.MoveNotations[^1]);

                var powersAfter = game.PowersAt(move.To);
                if (move.PromoteTo is null && (powersAfter & powersBefore) != powersBefore)
                    throw new XunitException($"Seed {seed}, ply {ply}: {move.San} lost powers {powersBefore} -> {powersAfter}.\n{string.Join(' ', log)}");

                AssertBothKingsAreStillOnTheBoard(game, seed, log);
            }

            Assert.True(game.IsGameOver, $"Seed {seed} never finished.\n{string.Join(' ', log)}");
            endReasons.Add(game.EndResult!.Value.Reason);
        }

        Assert.All(endReasons, reason => Assert.Contains(reason, (GameEndReason[])
        [
            GameEndReason.Checkmate, GameEndReason.Stalemate, GameEndReason.Repetition,
            GameEndReason.FiftyMoveRule, GameEndReason.MoveLimit,
        ]));
    }

    private static void AssertBothKingsAreStillOnTheBoard(GameState game, int seed, IReadOnlyList<string> log)
    {
        var placement = game.Fen.Split(' ')[0];
        if (placement.Count(symbol => symbol == 'K') != 1 || placement.Count(symbol => symbol == 'k') != 1)
            throw new XunitException($"Seed {seed}: a king went missing in {game.Fen}.\n{string.Join(' ', log)}");
    }
}
