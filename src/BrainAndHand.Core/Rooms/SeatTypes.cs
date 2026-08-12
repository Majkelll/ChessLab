using BrainAndHand.Core.Chess;

namespace BrainAndHand.Core.Rooms;

public enum SeatRole
{
    Brain,
    Hand,
}

/// <summary>One of the 4 seats in a room: a (side, role) pair.</summary>
public readonly record struct SeatId(Side Side, SeatRole Role)
{
    public override string ToString() => $"{Side}-{Role}";
}

public enum OccupantKind
{
    Empty,
    Human,
    Bot,
}

public enum BotDifficulty
{
    Easy,
    Medium,
    Hard,
    Expert,
}

public readonly record struct SeatOccupant(OccupantKind Kind, Guid? UserId, string? DisplayName, BotDifficulty? Difficulty)
{
    public static SeatOccupant Empty { get; } = new(OccupantKind.Empty, null, null, null);

    public static SeatOccupant Human(Guid userId, string displayName) =>
        new(OccupantKind.Human, userId, displayName, null);

    public static SeatOccupant Bot(BotDifficulty difficulty) =>
        new(OccupantKind.Bot, null, null, difficulty);
}
