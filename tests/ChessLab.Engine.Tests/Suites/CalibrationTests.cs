using ChessLab.Arena.Players;
using ChessLab.Arena.Suites;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Suites;

public class CalibrationTests
{
    [Fact]
    public async Task GreedyModeWhereHardAndExpertPlayAlike_FailsCalibration()
    {
        var gaps = await Calibration.MeasureAsync(GameKind.MartianChess, new ArenaSettings(),
            EngineData.Locate(null).Openings(GameKind.MartianChess), pairs: 20, concurrency: 4, CancellationToken.None);

        var hardToExpert = Assert.Single(gaps, gap => gap.Higher == BotDifficulty.Expert);
        Assert.False(hardToExpert.Passed);
        Assert.Contains("CALIBRATION FAILURE", hardToExpert.Describe());
        Assert.True(Assert.Single(gaps, gap => gap.Higher == BotDifficulty.Medium).Passed);
    }
}
