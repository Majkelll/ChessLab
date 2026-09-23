using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using GameState = ChessLab.Core.Martian.GameState;

namespace ChessLab.Bots;

/// <summary>Takes the points on offer and, failing that, keeps its pyramids at home: a piece pushed
/// across the middle for nothing is a piece handed to the opponent.</summary>
public sealed class MartianChessBot : IGameBot
{
    private readonly Random random = new();

    public Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var game = ((MartianChessSession)session).Game!;
        var move = GreedyPlay.Choose(game.AvailableMoves, difficulty, random, candidate => GivingAwayPenalty(seat.Side, candidate));
        return Task.FromResult(GameAction.MovePiece(move.From, move.To, move.PromoteTo));
    }

    private static int GivingAwayPenalty(Side side, ChessMove move) =>
        move.CapturedPiece is null && GameState.HalfOf(move.To) != side
            ? -6 * GreedyPlay.ValueOf(move.Piece)
            : 0;
}
