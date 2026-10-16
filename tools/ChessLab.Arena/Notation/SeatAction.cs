using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Notation;

public sealed record SeatAction(SeatId Seat, GameAction Action)
{
    public override string ToString() => ActionNotation.Format(this);
}
