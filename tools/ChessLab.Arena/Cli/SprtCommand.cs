using ChessLab.Arena.Matches;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;
using ChessLab.Arena.Statistics;

namespace ChessLab.Arena.Cli;

public sealed class SprtCommand : IArenaCommand
{
    public string Name => "sprt";

    public string Summary => "Run an SPRT of a candidate against a baseline and stop once it is decided.";

    public string Usage =>
        $"sprt --mode <mode> --candidate <player> --baseline <player> [--bounds 0,5] [--elo-model normalized|logistic]\n" +
        $"     [--alpha 0.05] [--beta 0.05] [--game-limit 40000] [--report <file>] {CommonOptions.Usage}\n" +
        $"  players: {PlayerSpecs.Help}";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var kind = Modes.Parse(args.Required("mode"));
        var settings = CommonOptions.Settings(args);
        var (elo0, elo1) = args.Bounds("bounds", (0, 5));
        var sprt = new Sprt(elo0, elo1,
            double.Parse(args.Optional("alpha") ?? "0.05", System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(args.Optional("beta") ?? "0.05", System.Globalization.CultureInfo.InvariantCulture),
            Enum.Parse<EloModel>(args.Optional("elo-model") ?? "normalized", ignoreCase: true),
            args.Int("game-limit", 40000));

        var report = await SprtRunner.RunAsync(sprt, new MatchSettings(
            kind,
            PlayerSpecs.Parse(args.Required("candidate"), settings),
            PlayerSpecs.Parse(args.Required("baseline"), settings),
            CommonOptions.Data(args).Openings(kind),
            sprt.GameLimit / 2,
            CommonOptions.Concurrency(args),
            args.Int("seed", 1),
            CommonOptions.BeforePair(args, output)), ct);

        var text = $"{sprt.Report(report.Match.Pentanomial, report.Outcome)}{report.Match.Describe()}";
        output.Write(text);
        if (args.Optional("report") is { } path)
            await File.WriteAllTextAsync(path, text, ct);

        return report.Outcome == SprtOutcome.H1Accepted ? 0 : 1;
    }
}
