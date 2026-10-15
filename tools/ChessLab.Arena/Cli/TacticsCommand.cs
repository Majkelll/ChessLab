using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;
using ChessLab.Arena.Suites;

namespace ChessLab.Arena.Cli;

public sealed class TacticsCommand : IArenaCommand
{
    public string Name => "tactics";

    public string Summary => "Report how many positions of the mode's tactics suite a player or engine solves.";

    public string Usage =>
        "tactics --mode <mode|all> [--player <player>|engine] [--depth 3] [--solved <file>] [--compare <file>] " +
        "[--stockfish <path>] [--budget-ms <n>] [--data <dir>]\n" +
        "  --solved   write the solved positions to a file\n" +
        "  --compare  fail when a position solved in that file is no longer solved\n" +
        "tactics --mode <mode> --generate [--count 12] [--depth 2]   print new candidate positions\n" +
        $"  players: {PlayerSpecs.Help}";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var modeText = args.Required("mode");
        var kinds = modeText == "all" ? Modes.All : [Modes.Parse(modeText)];
        var data = CommonOptions.Data(args);

        if (args.Has("generate"))
        {
            foreach (var kind in kinds)
                Generate(kind, data, args, output);
            return 0;
        }

        var previouslySolved = args.Optional("compare") is { } comparePath && File.Exists(comparePath)
            ? (await File.ReadAllLinesAsync(comparePath, ct)).ToHashSet()
            : null;
        var solvedLines = new List<string>();
        var regressions = 0;

        foreach (var kind in kinds)
        {
            var playerText = args.Optional("player") ?? "engine";
            ITacticSolver solver = playerText == "engine"
                ? new EngineSolver(args.Int("depth", 3))
                : new PlayerSolver(PlayerSpecs.Parse(playerText, CommonOptions.Settings(args)));

            var suite = data.Lines(kind, "tactics.txt").Select(TacticPosition.Parse).ToArray();
            var report = await Tactics.SolveAsync(kind, suite, solver, ct);
            output.WriteLine($"{Modes.FolderOf(kind)}: {report.Solved.Count} of {report.Total} solved by {report.Solver}");

            foreach (var (tactic, played) in report.Unsolved)
            {
                var key = $"{Modes.FolderOf(kind)} {tactic}";
                var regressed = previouslySolved?.Contains(key) == true;
                regressions += regressed ? 1 : 0;
                output.WriteLine($"  {(regressed ? "no longer solved" : "unsolved")}: {tactic} (played {played ?? "nothing"})");
            }

            solvedLines.AddRange(report.Solved.Select(tactic => $"{Modes.FolderOf(kind)} {tactic}"));
        }

        if (args.Optional("solved") is { } solvedPath)
            await File.WriteAllLinesAsync(solvedPath, solvedLines, ct);

        return regressions == 0 ? 0 : 1;
    }

    private static void Generate(Core.Rooms.GameKind kind, EngineData data, CommandArgs args, TextWriter output)
    {
        var candidates = TacticFinder.Find(kind, data.Openings(kind), args.Int("depth", 2), 80, args.Int("seed", 1))
            .Take(args.Int("count", 12));

        foreach (var candidate in candidates)
            output.WriteLine($"{candidate.Tactic}\t{candidate.PositionText}\tgain {candidate.Gain} margin {candidate.Margin}");
    }
}
