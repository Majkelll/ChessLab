using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Web.Client.Components;

/// <summary>What a game page needs from the shell around it: the state as it last arrived, which
/// seat the viewer is in, and the one way to do anything about it.</summary>
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
