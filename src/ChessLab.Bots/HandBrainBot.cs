using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

/// <summary>Decides bot moves for both Hand&amp;Brain roles on top of a single Stockfish engine.</summary>
public sealed class HandBrainBot(StockfishEngine engine) : IGameBot
{
    public async Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var game = ((GameSession)session).Game!;

        if (game.Phase == TurnPhase.BrainSelecting)
            return GameAction.SelectPieceKind(await ChooseBrainAnnouncementAsync(game, difficulty, ct));

        var move = await ChooseHandMoveAsync(game, difficulty, ct);
        return GameAction.MovePiece(move.From, move.To, move.PromoteTo);
    }

    /// <summary>
    /// For each piece kind with a legal move, finds that kind's best available move and its evaluation,
    /// then announces whichever kind yields the best outcome for the bot's side.
    /// </summary>
    public async Task<PieceKind> ChooseBrainAnnouncementAsync(GameState game, BotDifficulty difficulty, CancellationToken ct = default)
    {
        var preset = DifficultyPresets.For(difficulty);
        var fen = game.ToFen();
        var probeMovetimeMs = Math.Max(50, preset.MovetimeMs / 4);

        PieceKind? best = null;
        var bestScore = int.MinValue;

        foreach (var kind in game.AvailablePieceKinds())
        {
            var candidates = game.LegalMovesFor(kind).Select(m => m.ToUci()).Distinct().ToArray();
            var (_, score) = await engine.GoAsync(fen, candidates, preset.Elo, probeMovetimeMs, ct);

            if (score is { } s && s > bestScore)
            {
                bestScore = s;
                best = kind;
            }
        }

        return best ?? game.AvailablePieceKinds()[0];
    }

    public async Task<ChessMove> ChooseHandMoveAsync(GameState game, BotDifficulty difficulty, CancellationToken ct = default)
    {
        var preset = DifficultyPresets.For(difficulty);
        var moves = game.AvailableMoves();
        var candidates = moves.Select(m => m.ToUci()).Distinct().ToArray();

        var (bestUci, _) = await engine.GoAsync(game.ToFen(), candidates, preset.Elo, preset.MovetimeMs, ct);
        var (from, to, promotion) = UciNotation.ParseUciMove(bestUci);

        return moves.First(m => m.From == from && m.To == to && m.PromoteTo == promotion);
    }
}
