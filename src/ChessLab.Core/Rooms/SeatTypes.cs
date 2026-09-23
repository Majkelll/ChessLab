using ChessLab.Core.Chess;

namespace ChessLab.Core.Rooms;

public enum SeatRole
{
    Brain,
    Hand,

    Player,
}

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

public static class BotDifficultyElo
{
    public static int Approximate(BotDifficulty difficulty) => difficulty switch
    {
        BotDifficulty.Easy => 1320,
        BotDifficulty.Medium => 1700,
        BotDifficulty.Hard => 2100,
        BotDifficulty.Expert => 2800,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };
}

public readonly record struct SeatOccupant(OccupantKind Kind, Guid? UserId, string? DisplayName, BotDifficulty? Difficulty)
{
    public static SeatOccupant Empty { get; } = new(OccupantKind.Empty, null, null, null);

    public static SeatOccupant Human(Guid userId, string displayName) =>
        new(OccupantKind.Human, userId, displayName, null);

    public static SeatOccupant Bot(BotDifficulty difficulty) =>
        new(OccupantKind.Bot, null, null, difficulty);
}
