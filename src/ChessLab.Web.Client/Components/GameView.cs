using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Web.Client.Components;

public sealed class GameView
{
    public required RoomStateDto Room { get; init; }

    public required GameStateEnvelopeDto State { get; init; }

    public required Func<GameAction, Task> Perform { get; init; }

    public SeatId? MySeat { get; init; }

    public bool IsMySideToMove => MySeat is { } seat && seat.Side == State.SideToMove;

    public bool IsBoardInteractive => !State.IsGameOver && IsMySideToMove;

    public bool IsFlipped => MySeat?.Side == Side.Black;
}
