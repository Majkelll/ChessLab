using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

/// <summary>Every move of a series is an ordinary chess move in an ordinary chess position, so this
/// just asks Stockfish for each one in turn — it never sees that the same side is about to move
/// again, which is exactly the blind spot a human opponent gets to exploit.</summary>
public sealed class ProgressiveChessBot(StockfishEngine engine) : IGameBot
{
    public async Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var game = ((ProgressiveChessSession)session).Game!;
        var preset = DifficultyPresets.For(difficulty);
        var moves = game.AvailableMoves;
        var candidates = moves.Select(move => move.ToUci()).Distinct().ToArray();

        var (bestUci, _) = await engine.GoAsync(game.PositionText, candidates, preset.Elo, preset.MovetimeMs, ct);
        var (from, to, promotion) = UciNotation.ParseUciMove(bestUci);

        var chosen = moves.First(move => move.From == from && move.To == to && move.PromoteTo == promotion);
        return GameAction.MovePiece(chosen.From, chosen.To, chosen.PromoteTo);
    }
}
