using ChessLab.Arena.Notation;
using ChessLab.Arena.Suites;

namespace ChessLab.Arena.Cli;

public sealed class OpeningsCommand : IArenaCommand
{
    public string Name => "openings";

    public string Summary => "Generate a balanced opening suite by random playouts.";

    public string Usage => "openings --mode <mode|all> [--count 60] [--plies <n>] [--seed <n>] [--write] [--data <dir>]";

    public Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var modeText = args.Required("mode");
        var kinds = modeText == "all" ? Modes.All : [Modes.Parse(modeText)];
        var data = args.Has("write") ? CommonOptions.Data(args) : null;

        foreach (var kind in kinds)
        {
            ct.ThrowIfCancellationRequested();
            var openings = OpeningGenerator.Generate(kind, args.Int("count", 60),
                args.Int("plies", OpeningGenerator.DefaultPlies(kind)), args.Int("seed", 1));

            if (data is null)
            {
                foreach (var opening in openings)
                    output.WriteLine(opening);
                continue;
            }

            Directory.CreateDirectory(data.ModeDirectory(kind));
            File.WriteAllLines(data.FileOf(kind, "openings.txt"), openings.Select(opening => opening.ToString()));
            output.WriteLine($"{Modes.FolderOf(kind)}: wrote {openings.Count} openings");
        }

        return Task.FromResult(0);
    }
}
