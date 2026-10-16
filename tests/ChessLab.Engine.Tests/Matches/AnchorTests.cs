using ChessLab.Arena.Matches;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Matches;

[CollectionDefinition(nameof(AnchorTests), DisableParallelization = true)]
public class AnchorTimingCollection;

[Collection(nameof(AnchorTests))]
public class AnchorTests
{
    [Theory]
    [InlineData(GameKind.BiddingChess)]
    [InlineData(GameKind.MartianChess)]
    public async Task AnchorOfTheSameBuild_NeverLosesOnTimeAndScoresHalf(GameKind kind)
    {
        var settings = new ArenaSettings();
        var bot = new BotPlayerFactory(BotDifficulty.Expert, settings);
        var anchor = new AnchorPlayerFactory(AppContext.BaseDirectory, BotDifficulty.Expert, settings);

        var report = await MatchRunner.RunAsync(new MatchSettings(kind, bot, anchor, [PositionLine.Start()], Pairs: 3,
            Concurrency: 2));

        Assert.All(report.Pairs, pair =>
        {
            Assert.Null(pair.CandidateWhite.Forfeit);
            Assert.Null(pair.CandidateBlack.Forfeit);
        });
        Assert.Equal(0.5, report.Score);
    }
}
