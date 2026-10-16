using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public sealed class HandBrainBot(IChessEngine engine) : IGameBot
{
    public async Task<GameAction> ChooseActionAsync(GameStateEnvelopeDto state, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var section = state.HandBrain ?? throw new InvalidOperationException("The state has no Hand and Brain section.");

        if (section.Phase == TurnPhase.BrainSelecting)
            return GameAction.SelectPieceKind(await ChooseBrainAnnouncementAsync(state, section, difficulty, ct));

        var move = await EngineMoves.BestAsync(engine, state.PositionText, state.AvailableMoves,
            DifficultyPresets.For(difficulty), ct);
        return GameBots.MoveAction(move);
    }

    private async Task<PieceKind> ChooseBrainAnnouncementAsync(GameStateEnvelopeDto state, HandBrainSectionDto section,
        BotDifficulty difficulty, CancellationToken ct)
    {
        var preset = DifficultyPresets.For(difficulty);
        var rules = GeraChessRulesEngine.FromFen(state.PositionText);
        var probeMovetimeMs = Math.Max(50, preset.MovetimeMs / 4);

        PieceKind? best = null;
        var bestScore = int.MinValue;

        foreach (var kind in section.AvailablePieceKinds)
        {
            var candidates = rules.LegalMoves(kind).Select(move => move.ToUci()).Distinct().ToArray();
            var (_, score) = await engine.GoAsync(state.PositionText, candidates, preset.Elo, probeMovetimeMs, ct);

            if (score is { } s && s > bestScore)
            {
                bestScore = s;
                best = kind;
            }
        }

        return best ?? section.AvailablePieceKinds[0];
    }
}
