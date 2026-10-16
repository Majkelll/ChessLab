using ChessLab.Arena.Notation;
using ChessLab.Arena.Suites;

namespace ChessLab.Arena.Cli;

public sealed class CalibrateCommand : IArenaCommand
{
    public string Name => "calibrate";

    public string Summary => "Measure the Elo gap between consecutive difficulty levels and fail when a level is not stronger.";

    public string Usage => $"calibrate --mode <mode|all> [--pairs 100] {CommonOptions.Usage}";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var modeText = args.Required("mode");
        var kinds = modeText == "all" ? Modes.All : [Modes.Parse(modeText)];
        var data = CommonOptions.Data(args);
        var failures = 0;

        foreach (var kind in kinds)
        {
            var gaps = await Calibration.MeasureAsync(kind, CommonOptions.Settings(args), data.Openings(kind),
                args.Int("pairs", 100), CommonOptions.Concurrency(args), ct);

            foreach (var gap in gaps)
                output.WriteLine($"{Modes.FolderOf(kind)}: {gap.Describe()}");

            failures += gaps.Count(gap => !gap.Passed);
        }

        return failures == 0 ? 0 : 1;
    }
}
