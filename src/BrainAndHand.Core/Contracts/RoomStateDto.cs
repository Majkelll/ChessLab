using BrainAndHand.Core.Rooms;

namespace BrainAndHand.Core.Contracts;

public sealed record SeatSnapshot(SeatId Id, OccupantKind Kind, Guid? UserId, string? DisplayName, BotDifficulty? Difficulty);

public sealed record RoomStateDto(
    string Code,
    Guid HostUserId,
    GameKind Kind,
    IReadOnlyList<SeatSnapshot> Seats,
    /// <summary>True only while a game is actively in progress — false again once it ends
    /// (checkmate, resignation, timeout), not just before it ever started.</summary>
    bool HasStarted,
    /// <summary>True once a game has ever started in this room, win or not — stays true after
    /// the game ends, since the room's seats are permanently retired at that point (no rematch
    /// support yet). Lets the lobby tell "never played" apart from "game over" once HasStarted
    /// goes back to false.</summary>
    bool IsLocked,
    int InitialClockSeconds,
    int ClockIncrementSeconds);
