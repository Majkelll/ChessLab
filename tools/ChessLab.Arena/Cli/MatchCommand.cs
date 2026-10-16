using ChessLab.Arena.Matches;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;

namespace ChessLab.Arena.Cli;

public sealed class MatchCommand : IArenaCommand
{
    public string Name => "match";

    public string Summary => "Play paired games between two players and report the score.";

    public string Usage =>
        $"match --mode <mode> --candidate <player> --baseline <player> [--pairs <n>] {CommonOptions.Usage}\n" +
        $"  players: {PlayerSpecs.Help}";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var kind = Modes.Parse(args.Required("mode"));
        var settings = CommonOptions.Settings(args);
        var match = new MatchSettings(
            kind,
            PlayerSpecs.Parse(args.Required("candidate"), settings),
            PlayerSpecs.Parse(args.Required("baseline"), settings),
            CommonOptions.Data(args).Openings(kind),
            args.Int("pairs", 50),
            CommonOptions.Concurrency(args),
            args.Int("seed", 1),
            CommonOptions.BeforePair(args, output));

        var report = await MatchRunner.RunAsync(match, ct: ct);
        output.Write(report.Describe());
        return 0;
    }
}
