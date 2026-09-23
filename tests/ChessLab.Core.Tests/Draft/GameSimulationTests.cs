using ChessLab.Core.Chess;
using ChessLab.Core.Draft;
using ChessLab.Core.Rooms;
using Xunit.Sdk;
using GameState = ChessLab.Core.Draft.GameState;

namespace ChessLab.Core.Tests.Draft;

public class GameSimulationTests
{
    private static readonly PieceKind[] Pickable =
        [PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight, PieceKind.Pawn];

    [Fact]
    public void ManySimulatedGames_DraftAndLayOutLegalArmies_ThenPlayToAFinish()
    {
        var endReasons = new List<GameEndReason>();

        for (var seed = 0; seed < 30; seed++)
        {
            var rng = new Random(seed);
            var room = new Room("SIM", Guid.NewGuid(), GameKind.DraftChess);
            room.ClaimSeat(new SeatId(Side.White, SeatRole.Player), Guid.NewGuid(), "White");
            room.ClaimSeat(new SeatId(Side.Black, SeatRole.Player), Guid.NewGuid(), "Black");

            var session = new DraftChessSession(room);
            session.Start(TimeSpan.FromHours(1), TimeSpan.Zero, DateTimeOffset.UtcNow);
            var game = session.Game!;

            Draft(game, rng, seed);
            LayOut(game, rng, seed);

            Assert.Equal(DraftPhase.Playing, game.Phase);
            Assert.Equal(2, game.Fen!.Split(' ')[0].Count(symbol => symbol is 'K' or 'k'));

            for (var ply = 0; ply < GameState.MoveLimit + 10 && !game.IsGameOver; ply++)
            {
                var moves = game.AvailableMoves;
                if (moves.Count == 0)
                    throw new XunitException($"Seed {seed}: no legal move but the game is not over ({game.Fen}).");

                var move = moves[rng.Next(moves.Count)];
                session.MakeMove(move.From, move.To, move.PromoteTo, DateTimeOffset.UtcNow);
            }

            Assert.True(game.IsGameOver, $"Seed {seed} never finished ({game.Fen}).");
            endReasons.Add(game.EndResult!.Value.Reason);
        }

        Assert.All(endReasons, reason => Assert.Contains(reason, (GameEndReason[])
        [
            GameEndReason.Checkmate, GameEndReason.Stalemate, GameEndReason.Repetition,
            GameEndReason.FiftyMoveRule, GameEndReason.MoveLimit,
        ]));
        Assert.Contains(GameEndReason.Checkmate, endReasons);
    }

    private static void Draft(GameState game, Random rng, int seed)
    {
        var slots = 0;
        while (game.SideToPick is { } side)
        {
            if (slots++ > 100)
                throw new XunitException($"Seed {seed}: the draft never ended.");

            var affordable = Pickable.Where(kind => game.CanPick(side, kind)).ToArray();
            if (affordable.Length == 0 || rng.Next(12) == 0)
            {
                game.Pass(side);
                continue;
            }

            var pick = affordable[rng.Next(affordable.Length)];
            game.Pick(side, pick);

            if (game.BudgetLeft(side) < 0)
                throw new XunitException($"Seed {seed}: {side} overspent its budget.");
        }
    }

    private static void LayOut(GameState game, Random rng, int seed)
    {
        foreach (var side in (Side[])[Side.White, Side.Black])
        {
            var kinds = new List<PieceKind> { PieceKind.King };
            kinds.AddRange(game.PicksOf(side));

            foreach (var kind in kinds)
            {
                var squares = LegalSquaresFor(game, side, kind).ToArray();
                if (squares.Length == 0)
                    throw new XunitException($"Seed {seed}: nowhere left to put {side}'s {kind}.");

                game.Place(side, kind, squares[rng.Next(squares.Length)]);
            }
        }
    }

    private static IEnumerable<Square> LegalSquaresFor(GameState game, Side side, PieceKind kind)
    {
        var rank = kind == PieceKind.Pawn
            ? side == Side.White ? 1 : 6
            : side == Side.White ? 0 : 7;

        for (var file = 0; file < 8; file++)
        {
            var square = new Square(file, rank);
            if (!game.PlacementsOf(side).ContainsKey(square))
                yield return square;
        }
    }
}
