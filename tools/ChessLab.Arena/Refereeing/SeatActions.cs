using ChessLab.Arena.Notation;
using ChessLab.Core.Bidding;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Draft;
using ChessLab.Core.Games;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;
using DraftState = ChessLab.Core.Draft.GameState;

namespace ChessLab.Arena.Refereeing;

public static class SeatActions
{
    public static IReadOnlyList<SeatAction> For(GameStateEnvelopeDto state, SeatId seat)
    {
        if (state.IsGameOver)
            return [];

        var actions = state.Kind switch
        {
            GameKind.HandAndBrain => HandBrainActions(state),
            GameKind.BiddingChess => BiddingActions(state, seat.Side),
            GameKind.DraftChess => DraftActions(state, seat.Side),
            _ => Moves(state),
        };

        return [.. actions.Select(action => new SeatAction(seat, action))];
    }

    private static IEnumerable<GameAction> Moves(GameStateEnvelopeDto state) =>
        state.AvailableMoves.Select(move => GameAction.MovePiece(move.From, move.To, move.PromoteTo));

    private static IEnumerable<GameAction> HandBrainActions(GameStateEnvelopeDto state) =>
        state.HandBrain!.Phase == TurnPhase.BrainSelecting
            ? state.HandBrain.AvailablePieceKinds.Select(GameAction.SelectPieceKind)
            : Moves(state);

    private static IEnumerable<GameAction> BiddingActions(GameStateEnvelopeDto state, Side side)
    {
        var bidding = state.Bidding!;
        if (bidding.Phase == BiddingPhase.Moving)
            return Moves(state);

        var chips = side == Side.White ? bidding.WhiteChips : bidding.BlackChips;
        return Enumerable.Range(0, chips + 1).Select(GameAction.SubmitBid);
    }

    private static IEnumerable<GameAction> DraftActions(GameStateEnvelopeDto state, Side side)
    {
        var draft = state.Draft!;
        return draft.Phase switch
        {
            DraftPhase.Drafting => DraftPicks(draft, side),
            DraftPhase.Placing => (side == Side.White ? draft.WhitePlaceable : draft.BlackPlaceable)
                .SelectMany(entry => entry.Squares.Select(square => GameAction.PlacePiece(entry.Kind, square))),
            _ => Moves(state),
        };
    }

    private static IEnumerable<GameAction> DraftPicks(DraftSectionDto draft, Side side)
    {
        var budget = side == Side.White ? draft.WhiteBudgetLeft : draft.BlackBudgetLeft;
        var picks = side == Side.White ? draft.WhitePicks : draft.BlackPicks;
        var pawns = picks.Count(pick => pick == PieceKind.Pawn);
        var officers = picks.Count - pawns;

        foreach (var entry in draft.Pool)
        {
            var roomLeft = entry.Kind == PieceKind.Pawn ? pawns < DraftState.MaxPawns : officers < DraftState.MaxOfficers;
            if (entry.Remaining > 0 && entry.Cost <= budget && roomLeft)
                yield return GameAction.DraftPick(entry.Kind);
        }

        yield return GameAction.DraftPass();
    }
}
