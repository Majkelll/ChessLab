using BrainAndHand.Core.Rooms;

namespace BrainAndHand.Core.Contracts;

public sealed record SeatSnapshot(SeatId Id, OccupantKind Kind, Guid? UserId, string? DisplayName, BotDifficulty? Difficulty);

public sealed record RoomStateDto(
    string Code,
    Guid HostUserId,
    IReadOnlyList<SeatSnapshot> Seats,
    bool HasStarted,
    int InitialClockSeconds,
    int ClockIncrementSeconds);
