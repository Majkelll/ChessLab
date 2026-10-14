using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public sealed class AbsorptionChessBot : IGameBot
{
    private readonly Random random = new();

    public Task<GameAction> ChooseActionAsync(GameStateEnvelopeDto state, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default) =>
        Task.FromResult(GameBots.MoveAction(GreedyPlay.Choose(state.AvailableMoves, difficulty, random, AbsorptionBonus)));

    private static int AbsorptionBonus(ChessMove move) =>
        move.CapturedPiece is { } captured && captured != PieceKind.King
            ? 3 * GreedyPlay.ValueOf(captured)
            : 0;
}
