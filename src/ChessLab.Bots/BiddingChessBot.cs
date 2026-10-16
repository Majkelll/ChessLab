using ChessLab.Core.Bidding;
using ChessLab.Core.Board;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public sealed class BiddingChessBot(int? seed = null) : IGameBot
{
    private readonly Random random = BotRandom.For(seed);

    public Task<GameAction> ChooseActionAsync(GameStateEnvelopeDto state, SeatId seat, BotDifficulty difficulty,
        CancellationToken ct = default)
    {
        var section = state.Bidding ?? throw new InvalidOperationException("The state has no Bidding Chess section.");

        if (section.Phase == BiddingPhase.Bidding)
            return Task.FromResult(GameAction.SubmitBid(ChooseBid(state, section, seat.Side, difficulty)));

        return Task.FromResult(GameBots.MoveAction(GreedyPlay.Choose(state.AvailableMoves, difficulty, random)));
    }

    private int ChooseBid(GameStateEnvelopeDto state, BiddingSectionDto section, Side side, BotDifficulty difficulty)
    {
        var chips = side == Side.White ? section.WhiteChips : section.BlackChips;
        if (chips == 0)
            return 0;

        if (difficulty == BotDifficulty.Easy)
            return random.Next(0, Math.Min(chips, 20) + 1);

        var board = PieceBoard.FromFen(state.PositionText);

        if (CanTakeTheKing(board, side))
            return chips;

        if (CanTakeTheKing(board, side.Opposite()))
            return Math.Max(1, chips * 3 / 4);

        var prize = BestCaptureValue(board, side);
        var wanted = Math.Min(chips, 2 + (prize * 3));
        return Math.Max(0, wanted - random.Next(0, 3));
    }

    private static bool CanTakeTheKing(PieceBoard board, Side side) =>
        board.PseudoLegalMoves(side).Any(move => move.CapturedPiece == PieceKind.King);

    private static int BestCaptureValue(PieceBoard board, Side side) =>
        board.PseudoLegalMoves(side)
            .Select(move => move.CapturedPiece is { } captured ? GreedyPlay.ValueOf(captured) : 0)
            .DefaultIfEmpty(0)
            .Max();
}
