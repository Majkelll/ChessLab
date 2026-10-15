using ChessLab.Arena.Checks;
using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;

namespace ChessLab.Arena.Cli;

public sealed class FuzzCommand : IArenaCommand
{
    public string Name => "fuzz";

    public string Summary => "Play seeded random games and check the engine's legal actions against Core at every step.";

    public string Usage => "fuzz --mode <mode|all> [--games 20] [--seed 1] [--actions 400] [--out fuzz-failures]";

    public Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var modeText = args.Required("mode");
        var kinds = modeText == "all" ? Modes.All : [Modes.Parse(modeText)];
        var games = args.Int("games", 20);
        var firstSeed = args.Int("seed", 1);
        var actionLimit = args.Int("actions", DifferentialFuzz.DefaultActionLimit);
        var outDirectory = args.Optional("out") ?? "fuzz-failures";
        var failures = 0;

        foreach (var kind in kinds)
        {
            var engine = EngineRegistry.For(kind);
            for (var seed = firstSeed; seed < firstSeed + games; seed++)
            {
                ct.ThrowIfCancellationRequested();
                if (DifferentialFuzz.Run(engine, seed, actionLimit) is not { } failure)
                    continue;

                failures++;
                Directory.CreateDirectory(outDirectory);
                var path = Path.Combine(outDirectory, $"{Modes.FolderOf(kind)}-seed{seed}.txt");
                File.WriteAllText(path, failure.Describe() + Environment.NewLine);
                output.WriteLine($"{Modes.FolderOf(kind)}: mismatch with seed {seed}, written to {path}");
            }

            output.WriteLine($"{Modes.FolderOf(kind)}: fuzzed {games} games with {engine.Name}");
        }

        return Task.FromResult(failures == 0 ? 0 : 1);
    }
}
