using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Rooms;

public class BotDifficultyEloTests
{
    [Theory]
    [InlineData(BotDifficulty.Easy, 1320)]
    [InlineData(BotDifficulty.Medium, 1700)]
    [InlineData(BotDifficulty.Hard, 2100)]
    [InlineData(BotDifficulty.Expert, 2800)]
    public void Approximate_ReturnsTheExpectedRating(BotDifficulty difficulty, int expectedElo)
    {
        Assert.Equal(expectedElo, BotDifficultyElo.Approximate(difficulty));
    }

    [Fact]
    public void Approximate_IsStrictlyIncreasingWithDifficulty()
    {
        var ratings = Enum.GetValues<BotDifficulty>().Select(BotDifficultyElo.Approximate).ToArray();

        Assert.Equal(ratings.Order(), ratings);
        Assert.Equal(ratings.Distinct().Count(), ratings.Length);
    }
}
