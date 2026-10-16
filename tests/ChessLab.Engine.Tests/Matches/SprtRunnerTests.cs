using ChessLab.Arena.Matches;
using ChessLab.Arena.Players;
using ChessLab.Arena.Statistics;
using ChessLab.Arena.Suites;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Matches;

public class SprtRunnerTests
{
    private static readonly ArenaSettings Settings = new();

    [Fact]
    public async Task CandidateIdenticalToTheBaseline_IsNotAccepted()
    {
        var result = await RunAsync(new BotPlayerFactory(BotDifficulty.Hard, Settings),
            new BotPlayerFactory(BotDifficulty.Hard, Settings));

        Assert.Contains(result.Outcome, new[] { SprtOutcome.H0Accepted, SprtOutcome.Inconclusive });
    }

    [Fact]
    public async Task GreedyAgainstRandomMoves_AcceptsH1()
    {
        var result = await RunAsync(new BotPlayerFactory(BotDifficulty.Hard, Settings), new RandomPlayerFactory(Settings));

        Assert.Equal(SprtOutcome.H1Accepted, result.Outcome);
        Assert.True(result.Match.Games < 40000);
    }

    private static Task<SprtResult> RunAsync(IPlayerFactory candidate, IPlayerFactory baseline) =>
        SprtRunner.RunAsync(new Sprt(0, 5), new MatchSettings(GameKind.MartianChess, candidate, baseline,
            EngineData.Locate(null).Openings(GameKind.MartianChess), Pairs: 20000, Concurrency: 4), CancellationToken.None);
}
