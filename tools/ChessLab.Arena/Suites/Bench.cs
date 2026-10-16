using System.Diagnostics;
using System.Globalization;
using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Suites;

public sealed record BenchResult(GameKind Kind, string Engine, int Positions, int Depth, long Nodes, TimeSpan Elapsed)
{
    public double NodesPerSecond => Nodes / Math.Max(Elapsed.TotalSeconds, 1e-6);

    public string Describe() => string.Create(CultureInfo.InvariantCulture,
        $"{Modes.FolderOf(Kind)}: {Positions} positions, depth {Depth}, {Nodes} nodes, {NodesPerSecond:0} nodes per second ({Engine})");

    public string ToRecord() => string.Create(CultureInfo.InvariantCulture,
        $"{Modes.FolderOf(Kind)} {Nodes} {NodesPerSecond:0}");
}

public sealed record BenchExpectation(int Depth, long Nodes)
{
    public static BenchExpectation Parse(string text)
    {
        var values = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('='))
            .ToDictionary(pair => pair[0], pair => pair[1]);

        return new BenchExpectation(int.Parse(values["depth"], CultureInfo.InvariantCulture),
            long.Parse(values["nodes"], CultureInfo.InvariantCulture));
    }

    public override string ToString() => $"depth={Depth} nodes={Nodes}";
}

public static class Bench
{
    public const int PositionCount = 30;

    public static int DefaultDepth(GameKind kind) => kind switch
    {
        GameKind.BiddingChess => 1,
        GameKind.HandAndBrain => 3,
        _ => 2,
    };

    public static BenchResult Run(IEngineUnderTest engine, IReadOnlyList<PositionLine> positions, int depth,
        TimeSpan minimumTime = default)
    {
        var clock = Stopwatch.StartNew();
        var nodes = SearchAll(engine, positions, depth);
        if (minimumTime <= TimeSpan.Zero)
            return new BenchResult(engine.Kind, engine.Name, positions.Count, depth, nodes, clock.Elapsed);

        var passes = 0;
        clock.Restart();
        do
        {
            SearchAll(engine, positions, depth);
            passes++;
        }
        while (clock.Elapsed < minimumTime);

        return new BenchResult(engine.Kind, engine.Name, positions.Count, depth, nodes, clock.Elapsed / passes);
    }

    private static long SearchAll(IEngineUnderTest engine, IReadOnlyList<PositionLine> positions, int depth)
    {
        var nodes = 0L;
        foreach (var position in positions)
        {
            engine.Load(position);
            nodes += engine.Search(depth).Nodes;
        }

        return nodes;
    }

    public static IReadOnlyList<PositionLine> Generate(GameKind kind) =>
        OpeningGenerator.Generate(kind, PositionCount, 2 * OpeningGenerator.DefaultPlies(kind), seed: 2);
}
