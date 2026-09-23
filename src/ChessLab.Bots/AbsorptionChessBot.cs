using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

/// <summary>Greedy, with one Absorption-specific twist: a capture is worth more than the piece it
/// takes, because the capturing piece keeps those moves for the rest of the game — so taking a
/// queen with a pawn is the best thing that can happen to that pawn.</summary>
public sealed class AbsorptionChessBot : IGameBot
{
    private readonly Random random = new();

    public Task<GameAction> ChooseActionAsync(IRoomSession session, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var game = ((AbsorptionChessSession)session).Game!;
        var move = GreedyPlay.Choose(game.AvailableMoves, difficulty, random, AbsorptionBonus);
        return Task.FromResult(GameAction.MovePiece(move.From, move.To, move.PromoteTo));
    }

    private static int AbsorptionBonus(ChessMove move) =>
        move.CapturedPiece is { } captured && captured != PieceKind.King
            ? 3 * GreedyPlay.ValueOf(captured)
            : 0;
}
