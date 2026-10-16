using ChessLab.Arena.Matches;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;
using ChessLab.Arena.Suites;

namespace ChessLab.Arena.Cli;

public sealed class LadderCommand : IArenaCommand
{
    public string Name => "ladder";

    public string Summary => "Play players against anchors and append the Elo results to the ladder CSV.";

    public string Usage =>
        "ladder --mode <mode|all> [--players bot:Expert] [--opponent <player>] [--anchor-difficulty Expert]\n" +
        $"       [--root ~/chesslab-arena] [--csv <root>/ladder.csv] [--pairs 50] [--commit <label>] {CommonOptions.Usage}\n" +
        "  without --opponent every anchor of the mode is played";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var modeText = args.Required("mode");
        var kinds = modeText == "all" ? Modes.All : [Modes.Parse(modeText)];
        var settings = CommonOptions.Settings(args);
        var data = CommonOptions.Data(args);
        var root = args.Optional("root") ?? Anchors.DefaultRoot;
        var csv = args.Optional("csv") ?? Path.Combine(root, "ladder.csv");
        var commit = args.Optional("commit") ?? Environment.GetEnvironmentVariable("ARENA_COMMIT") ?? await CurrentCommitAsync(ct);
        var players = (args.Optional("players") ?? "bot:Expert").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var anchorDifficulty = args.Optional("anchor-difficulty") ?? "Expert";

        foreach (var kind in kinds)
        {
            var opponents = args.Optional("opponent") is { } opponent
                ? [opponent]
                : Anchors.Existing(root, kind).Select(directory => $"anchor:{directory}:{anchorDifficulty}").ToArray();

            if (opponents.Length == 0)
            {
                output.WriteLine($"{Modes.FolderOf(kind)}: no anchors under {root}, build one with 'arena anchor'");
                continue;
            }

            var rows = new List<string>();
            foreach (var player in players)
            {
                foreach (var opponentSpec in opponents)
                {
                    var match = await MatchRunner.RunAsync(new MatchSettings(kind,
                        PlayerSpecs.Parse(player, settings), PlayerSpecs.Parse(opponentSpec, settings),
                        data.Openings(kind), args.Int("pairs", 50), CommonOptions.Concurrency(args),
                        BeforePair: CommonOptions.BeforePair(args, output)), ct: ct);

                    var row = Ladder.Row(DateTimeOffset.UtcNow, commit, match);
                    rows.Add(row);
                    output.WriteLine(row);
                }
            }

            await Ladder.AppendAsync(csv, rows, ct);
        }

        return 0;
    }

    private static async Task<string> CurrentCommitAsync(CancellationToken ct)
    {
        try
        {
            return await Shell.RunAsync("git", ["rev-parse", "--short=12", "HEAD"], null, ct);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return "unknown";
        }
    }
}
