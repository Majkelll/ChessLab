using ChessLab.Core.Rooms;

namespace ChessLab.Bots;

public readonly record struct DifficultyPreset(int Elo, int MovetimeMs);

public static class DifficultyPresets
{
    public static DifficultyPreset For(BotDifficulty difficulty) => difficulty switch
    {
        BotDifficulty.Easy => new DifficultyPreset(BotDifficultyElo.Approximate(difficulty), MovetimeMs: 200),
        BotDifficulty.Medium => new DifficultyPreset(BotDifficultyElo.Approximate(difficulty), MovetimeMs: 500),
        BotDifficulty.Hard => new DifficultyPreset(BotDifficultyElo.Approximate(difficulty), MovetimeMs: 800),
        BotDifficulty.Expert => new DifficultyPreset(BotDifficultyElo.Approximate(difficulty), MovetimeMs: 1200),
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };
}
