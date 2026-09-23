using ChessLab.Core.Rooms;

namespace ChessLab.Core.Contracts;

public sealed record SeatSnapshot(SeatId Id, OccupantKind Kind, Guid? UserId, string? DisplayName, BotDifficulty? Difficulty);

public sealed record RoomStateDto(
    string Code,
    Guid HostUserId,
    GameKind Kind,
    IReadOnlyList<SeatSnapshot> Seats,
    bool HasStarted,
    bool IsLocked,
    int InitialClockSeconds,
    int ClockIncrementSeconds);
