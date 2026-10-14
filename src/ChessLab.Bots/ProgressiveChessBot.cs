using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public sealed class ProgressiveChessBot(IChessEngine engine) : IGameBot
{
    public async Task<GameAction> ChooseActionAsync(GameStateEnvelopeDto state, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var move = await EngineMoves.BestAsync(engine, state.PositionText, state.AvailableMoves,
            DifficultyPresets.For(difficulty), ct);
        return GameBots.MoveAction(move);
    }
}
