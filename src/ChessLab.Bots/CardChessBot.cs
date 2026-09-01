using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

/// <summary>Decides Card Chess bot moves on top of a single Stockfish engine. Unlike Hand &amp;
/// Brain's bot, there's only ever one decision to make per turn: the card (or Emergency Move)
/// already narrows the board down to <see cref="GameState.AvailableMoves"/> before the bot is
/// asked anything, so this is just "pick the best of these candidates" — the same technique
/// <see cref="HandBrainBot.ChooseHandMoveAsync"/> uses for its Hand role.</summary>
public sealed class CardChessBot(StockfishEngine engine)
{
    public async Task<ChessMove> ChooseMoveAsync(GameState game, BotDifficulty difficulty, CancellationToken ct = default)
    {
        var preset = DifficultyPresets.For(difficulty);
        var moves = game.AvailableMoves;
        var candidates = moves.Select(m => m.ToUci()).Distinct().ToArray();

        var (bestUci, _) = await engine.GoAsync(game.ToFen(), candidates, preset.Elo, preset.MovetimeMs, ct);
        var (from, to, promotion) = UciNotation.ParseUciMove(bestUci);

        return moves.First(m => m.From == from && m.To == to && m.PromoteTo == promotion);
    }
}
