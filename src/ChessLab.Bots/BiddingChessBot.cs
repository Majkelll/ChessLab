using ChessLab.Core.Bidding;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public sealed class BiddingChessBot : IGameBot
{
    private readonly Random random = new();

    public Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var game = ((BiddingChessSession)session).Game!;

        if (game.Phase == BiddingPhase.Bidding)
            return Task.FromResult(GameAction.SubmitBid(ChooseBid(game, seat.Side, difficulty)));

        var move = GreedyPlay.Choose(game.AvailableMoves, difficulty, random);
        return Task.FromResult(GameAction.MovePiece(move.From, move.To, move.PromoteTo));
    }

    private int ChooseBid(GameState game, Side side, BotDifficulty difficulty)
    {
        var chips = game.ChipsOf(side);
        if (chips == 0)
            return 0;

        if (difficulty == BotDifficulty.Easy)
            return random.Next(0, Math.Min(chips, 20) + 1);

        if (CanTakeTheKing(game, side))
            return chips;

        if (CanTakeTheKing(game, Opponent(side)))
            return Math.Max(1, chips * 3 / 4);

        var prize = BestCaptureValue(game, side);
        var wanted = Math.Min(chips, 2 + (prize * 3));
        return Math.Max(0, wanted - random.Next(0, 3));
    }

    private static bool CanTakeTheKing(GameState game, Side side) =>
        game.MovesFor(side).Any(move => move.CapturedPiece == PieceKind.King);

    private static int BestCaptureValue(GameState game, Side side) =>
        game.MovesFor(side)
            .Select(move => move.CapturedPiece is { } captured ? GreedyPlay.ValueOf(captured) : 0)
            .DefaultIfEmpty(0)
            .Max();

    private static Side Opponent(Side side) => side == Side.White ? Side.Black : Side.White;
}
