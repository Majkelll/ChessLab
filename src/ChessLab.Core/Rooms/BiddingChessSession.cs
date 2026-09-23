using ChessLab.Core.Bidding;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using GameState = ChessLab.Core.Bidding.GameState;

namespace ChessLab.Core.Rooms;

public sealed class BiddingChessSession(Room room) : RoomSession<GameState>(room)
{
    public override IReadOnlyList<SeatId> ActiveSeats
    {
        get
        {
            var game = StartedGame;
            if (game.Phase == BiddingPhase.Moving)
                return [new SeatId(game.SideToMove, SeatRole.Player)];

            return
            [
                .. new[] { Side.White, Side.Black }
                    .Where(side => !game.HasBid(side))
                    .Select(side => new SeatId(side, SeatRole.Player)),
            ];
        }
    }

    public override void Start(TimeSpan initial, TimeSpan increment, DateTimeOffset now, Random? random = null)
    {
        EnsureNotStarted();
        Room.Lock();
        Game = new GameState(new Clock(initial, increment));
        TurnStartedAt = now;
    }

    public override void Apply(GameAction action, SeatId seat, DateTimeOffset now)
    {
        switch (action.Kind)
        {
            case GameActionKind.SubmitBid:
                SubmitBid(seat.Side, action.Amount!.Value, now);
                break;
            case GameActionKind.Move:
                MakeMove(action.From!.Value, action.To!.Value, action.PromoteTo, now);
                break;
            default:
                throw new InvalidOperationException($"{action.Kind} is not an action in Bidding Chess.");
        }
    }

    public void SubmitBid(Side side, int amount, DateTimeOffset now)
    {
        var game = StartedGame;
        game.SubmitBid(side, amount, ElapsedSinceTurnStart(now));

        if (game.Phase == BiddingPhase.Moving)
            TurnStartedAt = now;
    }

    public ChessMove MakeMove(Square from, Square to, PieceKind? promoteTo, DateTimeOffset now)
    {
        var move = StartedGame.MakeMove(from, to, promoteTo, ElapsedSinceTurnStart(now));
        TurnStartedAt = now;
        return move;
    }

    protected override IReadOnlyList<ChessMove> AvailableMoves => StartedGame.AvailableMoves;

    protected override IReadOnlyList<ChessMove> MoveHistory => StartedGame.MoveHistory;

    protected override BiddingSectionDto BiddingSection
    {
        get
        {
            var game = StartedGame;
            return new BiddingSectionDto(
                game.Phase,
                game.ChipsOf(Side.White),
                game.ChipsOf(Side.Black),
                game.MarkerHolder,
                game.HasBid(Side.White),
                game.HasBid(Side.Black),
                game.LastWhiteBid,
                game.LastBlackBid);
        }
    }
}
