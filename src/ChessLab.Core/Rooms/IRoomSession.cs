namespace ChessLab.Core.Rooms;

/// <summary>The bit every game-kind's session shares — enough for room-management (join, seats,
/// clock settings) and the lobby/clock-watchdog to work without knowing which game is being played.</summary>
public interface IRoomSession
{
    Room Room { get; }

    /// <summary>True once a game has started and is still in progress — mirrors <c>Game is { IsGameOver: false }</c>
    /// on whichever concrete game-state type this session holds.</summary>
    bool HasActiveGame { get; }
}
