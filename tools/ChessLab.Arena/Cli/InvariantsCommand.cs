using ChessLab.Arena.Checks;
using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;

namespace ChessLab.Arena.Cli;

public sealed class InvariantsCommand : IArenaCommand
{
    public string Name => "invariants";

    public string Summary => "Check make/unmake, incremental hashing, mirrored evaluation and allocation-free search.";

    public string Usage => "invariants --mode <mode|all> [--positions 10] [--plies 16] [--depth 2] [--seed 1] [--data <dir>]";

    public Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var modeText = args.Required("mode");
        var kinds = modeText == "all" ? Modes.All : [Modes.Parse(modeText)];
        var data = CommonOptions.Data(args);
        var broken = 0;

        foreach (var kind in kinds)
        {
            ct.ThrowIfCancellationRequested();
            var engine = EngineRegistry.For(kind);
            var positions = data.Openings(kind).Take(args.Int("positions", 10)).ToArray();
            var violations = Invariants.Check(engine, positions, args.Int("seed", 1), args.Int("plies", 16)).ToList();

            if (engine.AllocationFreeSearch)
            {
                if (Invariants.CheckSearchAllocation(engine, positions[0], args.Int("depth", 2)) is { } allocation)
                    violations.Add(allocation);
            }
            else
            {
                output.WriteLine($"{Modes.FolderOf(kind)}: allocation check skipped, {engine.Name} does not claim an allocation-free search");
            }

            foreach (var violation in violations)
                output.WriteLine($"{Modes.FolderOf(kind)}: {violation.Describe()}");

            broken += violations.Count;
            output.WriteLine($"{Modes.FolderOf(kind)}: {violations.Count} invariant violations ({engine.Name})");
        }

        return Task.FromResult(broken == 0 ? 0 : 1);
    }
}
