using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public sealed class AliceChessBot : IGameBot
{
    private readonly Random random = new();

    public Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var game = ((AliceChessSession)session).Game!;
        var move = GreedyPlay.Choose(game.AvailableMoves, difficulty, random);
        return Task.FromResult(GameAction.MovePiece(move.From, move.To, move.PromoteTo));
    }
}
