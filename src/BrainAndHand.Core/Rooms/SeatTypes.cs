using BrainAndHand.Core.Chess;

namespace BrainAndHand.Core.Rooms;

public enum SeatRole
{
    Brain,
    Hand,

    /// <summary>The only role in games with no Brain/Hand split (e.g. Card Chess) — one seat per side.</summary>
    Player,
}

/// <summary>One seat in a room: a (side, role) pair. Hand &amp; Brain rooms have 4 (White/Black x
/// Brain/Hand); other game kinds may have fewer — see <see cref="Room.SeatIds"/>.</summary>
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

/// <summary>
/// Approximate playing strength for each difficulty, shown in the lobby so a bot's difficulty
/// means something concrete instead of just a label — "Easy" on its own says nothing, but "1320
/// Elo" does. These aren't decorative: <c>BrainAndHand.Bots</c> configures Stockfish's own
/// <c>UCI_Elo</c> strength limiter with these exact values, so the number is the actual target,
/// not just a guess at what it might play like. Kept in Core (rather than the Bots project,
/// which the WASM client can't reference) so the lobby UI and the engine share one source of
/// truth instead of two numbers that could drift apart.
/// </summary>
public static class BotDifficultyElo
{
    // Stockfish's UCI_Elo has supported roughly this 1320-3190 range since the strength limiter
    // was introduced in Stockfish 11 and has stayed stable since.
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
