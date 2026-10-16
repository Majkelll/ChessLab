using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Suites;
using ChessLab.Core.Rooms;
using ChessLab.Engine.Tests.Engines;

namespace ChessLab.Engine.Tests.Suites;

public class BenchTests
{
    public static TheoryData<GameKind> AllModes => [.. Modes.All];

    [Fact]
    public void RepeatedBench_CountsTheSameNodes()
    {
        var positions = EngineData.Locate(null).Lines(GameKind.MartianChess, "bench.txt").Select(PositionLine.Parse).ToArray();

        var first = Bench.Run(new SampleMartianEngine(), positions, 4);
        var second = Bench.Run(new SampleMartianEngine(), positions, 4);

        Assert.Equal(first.Nodes, second.Nodes);
        Assert.True(first.Nodes > positions.Length);
    }

    [Fact]
    public void MinimumTime_RepeatsThePositionsButReportsTheNodesAndTimeOfOnePass()
    {
        var positions = EngineData.Locate(null).Lines(GameKind.MartianChess, "bench.txt").Select(PositionLine.Parse).ToArray();
        var minimum = TimeSpan.FromMilliseconds(500);

        var single = Bench.Run(new SampleMartianEngine(), positions, 1);
        var repeated = Bench.Run(new SampleMartianEngine(), positions, 1, minimum);

        Assert.Equal(single.Nodes, repeated.Nodes);
        Assert.True(repeated.Elapsed < minimum / 2);
    }

    [Fact]
    public void BehaviourChange_ChangesTheNodeCount()
    {
        var positions = EngineData.Locate(null).Lines(GameKind.MartianChess, "bench.txt").Select(PositionLine.Parse).ToArray();

        var correct = Bench.Run(new SampleMartianEngine(), positions, 4);
        var changed = Bench.Run(new SampleMartianEngine(MartianBug.EvaluationIgnoresMaterial), positions, 4);

        Assert.NotEqual(correct.Nodes, changed.Nodes);
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void EveryMode_HasBenchPositionsAndAnExpectedNodeCount(GameKind kind)
    {
        var data = EngineData.Locate(null);
        var positions = data.Lines(kind, "bench.txt").Select(PositionLine.Parse).ToArray();
        var expected = BenchExpectation.Parse(File.ReadAllText(data.FileOf(kind, "bench.expected")));

        Assert.Equal(Bench.PositionCount, positions.Length);
        Assert.True(expected.Nodes > 0);
    }

    [Fact]
    public void ReferenceBench_MatchesBenchExpected()
    {
        var data = EngineData.Locate(null);
        var positions = data.Lines(GameKind.MartianChess, "bench.txt").Select(PositionLine.Parse).ToArray();
        var expected = BenchExpectation.Parse(File.ReadAllText(data.FileOf(GameKind.MartianChess, "bench.expected")));

        var result = Bench.Run(EngineRegistry.For(GameKind.MartianChess), positions, expected.Depth);

        Assert.Equal(expected.Nodes, result.Nodes);
    }
}
