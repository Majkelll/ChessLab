using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

/// <summary>Plays Alice Chess one move deep: take the best thing available, and prefer keeping
/// pieces on the board the opponent's king is not on, which is as much long-term plan as a
/// one-move search can hold.</summary>
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
