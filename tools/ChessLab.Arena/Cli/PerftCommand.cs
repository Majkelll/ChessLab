using ChessLab.Arena.Checks;
using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Cli;

public sealed class PerftCommand : IArenaCommand
{
    private const int ReferencePositions = 5;

    public string Name => "perft";

    public string Summary => "Count move-generation trees and compare the engine with Core's reference counts.";

    public string Usage =>
        "perft --mode <mode|all> [--reference [--write]] [--position \"<line>\"] [--depth <n>] [--data <dir>]\n" +
        "  --reference  compute counts from Core clones (with --write: store them in perft.txt)\n" +
        "  --position   count one position and list the count below each action";

    public static int DefaultDepth(GameKind kind) => kind switch
    {
        GameKind.BiddingChess => 2,
        GameKind.HandAndBrain => 4,
        _ => 3,
    };

    public Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var modeText = args.Required("mode");
        var kinds = modeText == "all" ? Modes.All : [Modes.Parse(modeText)];
        var failed = false;

        foreach (var kind in kinds)
        {
            ct.ThrowIfCancellationRequested();
            var depth = args.Int("depth", DefaultDepth(kind));

            if (args.Optional("position") is { } position)
                Divide(EngineRegistry.For(kind), PositionLine.Parse(position), depth, output);
            else if (args.Has("reference"))
                WriteReference(kind, depth, args, output);
            else
                failed |= !Check(kind, args, output);
        }

        return Task.FromResult(failed ? 1 : 0);
    }

    private static void Divide(IEngineUnderTest engine, PositionLine position, int depth, TextWriter output)
    {
        engine.Load(position);
        var total = 0L;
        foreach (var action in engine.LegalActions().Order(StringComparer.Ordinal))
        {
            engine.Apply(action);
            var nodes = depth > 1 ? Perft.Count(engine, depth - 1) : 1;
            engine.Undo();
            total += nodes;
            output.WriteLine($"{action} {nodes}");
        }

        output.WriteLine($"total {total}");
    }

    private static void WriteReference(GameKind kind, int depth, CommandArgs args, TextWriter output)
    {
        var data = CommonOptions.Data(args);
        var positions = data.Openings(kind).Take(ReferencePositions - 1).Prepend(PositionLine.Start());
        var reference = new CoreReferenceEngine(kind);
        var lines = positions
            .Select(position => new PerftReference(depth, Perft.Count(reference, position, depth), position).ToString())
            .ToArray();

        if (args.Has("write"))
        {
            File.WriteAllLines(data.FileOf(kind, "perft.txt"), lines);
            output.WriteLine($"{Modes.FolderOf(kind)}: wrote {lines.Length} perft references");
            return;
        }

        foreach (var line in lines)
            output.WriteLine(line);
    }

    private static bool Check(GameKind kind, CommandArgs args, TextWriter output)
    {
        var engine = EngineRegistry.For(kind);
        var passed = true;

        foreach (var reference in CommonOptions.Data(args).Lines(kind, "perft.txt").Select(PerftReference.Parse))
        {
            var nodes = Perft.Count(engine, reference.Position, reference.Depth);
            if (nodes == reference.Nodes)
                continue;

            passed = false;
            output.WriteLine($"{Modes.FolderOf(kind)}: {engine.Name} counts {nodes} at depth {reference.Depth} " +
                             $"instead of {reference.Nodes} in '{reference.Position}'.");
            var mismatch = Perft.FirstDifference(engine, new CoreReferenceEngine(kind), reference.Position, reference.Depth);
            output.WriteLine(mismatch?.Describe() ?? "  Core agrees with the engine, so perft.txt is out of date.");
        }

        if (passed)
            output.WriteLine($"{Modes.FolderOf(kind)}: perft matches ({engine.Name})");

        return passed;
    }
}
