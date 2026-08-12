using BrainAndHand.Core.Chess;
using BrainAndHand.Core.HandBrain;
using BrainAndHand.Core.Rooms;

namespace BrainAndHand.Bots;

/// <summary>Decides bot moves for both Hand&amp;Brain roles on top of a single Stockfish engine.</summary>
public sealed class HandBrainBot(StockfishEngine engine)
{
    /// <summary>
    /// For each piece kind with a legal move, finds that kind's best available move and its evaluation,
    /// then announces whichever kind yields the best outcome for the bot's side.
    /// </summary>
    public async Task<PieceKind> ChooseBrainAnnouncementAsync(GameState game, BotDifficulty difficulty, CancellationToken ct = default)
    {
        var preset = DifficultyPresets.For(difficulty);
        var fen = game.ToFen();

        PieceKind? best = null;
        var bestScore = int.MinValue;

        foreach (var kind in game.AvailablePieceKinds())
        {
            var candidates = game.LegalMovesFor(kind).Select(m => m.ToUci()).Distinct().ToArray();
            var (_, score) = await engine.GoAsync(fen, candidates, preset.SkillLevel, preset.MovetimeMs, ct);

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

        var (bestUci, _) = await engine.GoAsync(game.ToFen(), candidates, preset.SkillLevel, preset.MovetimeMs, ct);
        var (from, to, promotion) = UciNotation.ParseUciMove(bestUci);

        return moves.First(m => m.From == from && m.To == to && m.PromoteTo == promotion);
    }
}
