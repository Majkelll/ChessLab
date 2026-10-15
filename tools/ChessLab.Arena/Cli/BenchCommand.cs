using System.Globalization;
using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Suites;

namespace ChessLab.Arena.Cli;

public sealed class BenchCommand : IArenaCommand
{
    public string Name => "bench";

    public string Summary => "Search the bench positions to a fixed depth on one thread; print nodes and nodes per second.";

    public string Usage =>
        "bench --mode <mode|all> [--depth <n>] [--check] [--write] [--generate] [--best-of 1] [--output <file>] [--data <dir>]\n" +
        "  --check     fail when the node count differs from bench.expected\n" +
        "  --write     store the node count in bench.expected\n" +
        "  --generate  write new bench.txt positions first\n" +
        "  --output    append 'mode nodes nps' records for bench-compare";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var modeText = args.Required("mode");
        var kinds = modeText == "all" ? Modes.All : [Modes.Parse(modeText)];
        var data = CommonOptions.Data(args);
        var failed = false;
        var records = new List<string>();

        foreach (var kind in kinds)
        {
            ct.ThrowIfCancellationRequested();
            if (args.Has("generate"))
                await File.WriteAllLinesAsync(data.FileOf(kind, "bench.txt"),
                    Bench.Generate(kind).Select(position => position.ToString()), ct);

            var expectedPath = data.FileOf(kind, "bench.expected");
            var expected = File.Exists(expectedPath)
                ? BenchExpectation.Parse(await File.ReadAllTextAsync(expectedPath, ct))
                : null;
            var depth = args.Int("depth", expected?.Depth ?? Bench.DefaultDepth(kind));
            var positions = data.Lines(kind, "bench.txt").Select(PositionLine.Parse).ToArray();

            var result = Enumerable.Range(0, Math.Max(1, args.Int("best-of", 1)))
                .Select(_ => Bench.Run(EngineRegistry.For(kind), positions, depth))
                .MaxBy(run => run.NodesPerSecond)!;

            output.WriteLine(result.Describe());
            records.Add(result.ToRecord());

            if (args.Has("write"))
                await File.WriteAllTextAsync(expectedPath, new BenchExpectation(depth, result.Nodes) + Environment.NewLine, ct);
            else if (args.Has("check") && expected is not null && (expected.Nodes != result.Nodes || expected.Depth != depth))
            {
                failed = true;
                output.WriteLine($"{Modes.FolderOf(kind)}: bench.expected says {expected}, the engine searched " +
                                 string.Create(CultureInfo.InvariantCulture, $"depth={depth} nodes={result.Nodes}"));
            }
            else if (args.Has("check") && expected is null)
            {
                failed = true;
                output.WriteLine($"{Modes.FolderOf(kind)}: {expectedPath} is missing");
            }
        }

        if (args.Optional("output") is { } path)
            await File.AppendAllLinesAsync(path, records, ct);

        return failed ? 1 : 0;
    }
}
