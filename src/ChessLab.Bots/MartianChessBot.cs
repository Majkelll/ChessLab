using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using GameState = ChessLab.Core.Martian.GameState;

namespace ChessLab.Bots;

public sealed class MartianChessBot(int? seed = null) : IGameBot
{
    private readonly Random random = BotRandom.For(seed);

    public Task<GameAction> ChooseActionAsync(GameStateEnvelopeDto state, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var move = GreedyPlay.Choose(state.AvailableMoves, BotDifficulty.Easy, random, candidate => GivingAwayPenalty(seat.Side, candidate));
        return Task.FromResult(GameBots.MoveAction(move));
    }

    private static int GivingAwayPenalty(Side side, ChessMove move) =>
        move.CapturedPiece is null && GameState.HalfOf(move.To) != side
            ? -6 * GreedyPlay.ValueOf(move.Piece)
            : 0;
}
