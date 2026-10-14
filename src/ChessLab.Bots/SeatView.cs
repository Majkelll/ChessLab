using ChessLab.Core.Bidding;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Draft;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public static class SeatView
{
    public static bool CanAct(GameStateEnvelopeDto state, SeatId seat)
    {
        if (state.IsGameOver)
            return false;

        return state.Kind switch
        {
            GameKind.HandAndBrain => state.HandBrain is { } section && seat.Side == state.SideToMove &&
                seat.Role == (section.Phase == TurnPhase.BrainSelecting ? SeatRole.Brain : SeatRole.Hand),
            GameKind.BiddingChess => state.Bidding is { } section && seat.Role == SeatRole.Player &&
                BiddingSeatCanAct(state, section, seat.Side),
            GameKind.DraftChess => state.Draft is { } section && seat.Role == SeatRole.Player &&
                DraftSeatCanAct(state, section, seat.Side),
            _ => seat.Role == SeatRole.Player && seat.Side == state.SideToMove,
        };
    }

    private static bool BiddingSeatCanAct(GameStateEnvelopeDto state, BiddingSectionDto section, Side side) =>
        section.Phase == BiddingPhase.Moving
            ? side == state.SideToMove
            : !(side == Side.White ? section.WhiteHasBid : section.BlackHasBid);

    private static bool DraftSeatCanAct(GameStateEnvelopeDto state, DraftSectionDto section, Side side) =>
        section.Phase switch
        {
            DraftPhase.Drafting => section.SideToPick == side,
            DraftPhase.Placing => !(side == Side.White ? section.WhiteReady : section.BlackReady),
            _ => side == state.SideToMove,
        };
}
