using BrainAndHand.Core.Rooms;

namespace BrainAndHand.Bots;

public readonly record struct DifficultyPreset(int SkillLevel, int MovetimeMs);

public static class DifficultyPresets
{
    public static DifficultyPreset For(BotDifficulty difficulty) => difficulty switch
    {
        BotDifficulty.Easy => new DifficultyPreset(SkillLevel: 2, MovetimeMs: 200),
        BotDifficulty.Medium => new DifficultyPreset(SkillLevel: 8, MovetimeMs: 500),
        BotDifficulty.Hard => new DifficultyPreset(SkillLevel: 14, MovetimeMs: 800),
        BotDifficulty.Expert => new DifficultyPreset(SkillLevel: 20, MovetimeMs: 1200),
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty)),
    };
}
