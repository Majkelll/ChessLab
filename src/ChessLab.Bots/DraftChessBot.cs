using ChessLab.Core.Chess;
using ChessLab.Core.Draft;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using GameState = ChessLab.Core.Draft.GameState;

namespace ChessLab.Bots;

public sealed class DraftChessBot : IGameBot
{
    private static readonly PieceKind[] ByWeight =
        [PieceKind.Queen, PieceKind.Rook, PieceKind.Bishop, PieceKind.Knight, PieceKind.Pawn];

    private static readonly int[] BackRankOrder = [4, 3, 5, 2, 6, 1, 7, 0];

    private readonly Random random = new();

    public Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var game = ((DraftChessSession)session).Game!;

        return Task.FromResult(game.Phase switch
        {
            DraftPhase.Drafting => ChoosePick(game, seat.Side, difficulty),
            DraftPhase.Placing => ChoosePlacement(game, seat.Side),
            _ => ChooseMove(game, difficulty),
        });
    }

    private GameAction ChoosePick(GameState game, Side side, BotDifficulty difficulty)
    {
        var affordable = ByWeight.Where(kind => game.CanPick(side, kind)).ToArray();
        if (affordable.Length == 0)
            return GameAction.DraftPass();

        if (difficulty == BotDifficulty.Easy)
            return GameAction.DraftPick(affordable[random.Next(affordable.Length)]);

        return GameAction.DraftPick(affordable[0]);
    }

    private static GameAction ChoosePlacement(GameState game, Side side)
    {
        var backRank = side == Side.White ? 0 : 7;
        var pawnRank = side == Side.White ? 1 : 6;

        if (game.Remaining(side, PieceKind.King) > 0)
            return GameAction.PlacePiece(PieceKind.King, FirstFree(game, side, backRank));

        foreach (var kind in ByWeight)
        {
            if (game.Remaining(side, kind) == 0)
                continue;

            var rank = kind == PieceKind.Pawn ? pawnRank : backRank;
            return GameAction.PlacePiece(kind, FirstFree(game, side, rank));
        }

        throw new InvalidOperationException("Everything is already laid out.");
    }

    private static Square FirstFree(GameState game, Side side, int rank)
    {
        foreach (var file in BackRankOrder)
        {
            var square = new Square(file, rank);
            if (!game.PlacementsOf(side).ContainsKey(square))
                return square;
        }

        throw new InvalidOperationException($"Rank {rank + 1} is full.");
    }

    private GameAction ChooseMove(GameState game, BotDifficulty difficulty)
    {
        var move = GreedyPlay.Choose(game.AvailableMoves, difficulty, random);
        return GameAction.MovePiece(move.From, move.To, move.PromoteTo);
    }
}
