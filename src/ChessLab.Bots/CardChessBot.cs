using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public sealed class CardChessBot(StockfishEngine engine) : IGameBot
{
    public async Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var move = await ChooseMoveAsync(((CardChessSession)session).Game!, difficulty, ct);
        return GameAction.MovePiece(move.From, move.To, move.PromoteTo);
    }

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
