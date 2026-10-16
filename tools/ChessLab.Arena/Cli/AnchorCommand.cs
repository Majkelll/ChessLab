using ChessLab.Arena.Notation;
using ChessLab.Arena.Suites;

namespace ChessLab.Arena.Cli;

public sealed class AnchorCommand : IArenaCommand
{
    public string Name => "anchor";

    public string Summary => "Build a frozen bot version pinned by commit into <root>/anchors/<mode>/<hash>.";

    public string Usage =>
        "anchor [--commit HEAD] [--mode all] [--root ~/chesslab-arena] [--label <name>] [--working-tree] [--repo <dir>]\n" +
        "  --working-tree  publish the current working tree instead of a clean checkout (labelled <hash>-worktree)";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var modeText = args.Optional("mode") ?? "all";
        var kinds = modeText == "all" ? Modes.All : [Modes.Parse(modeText)];
        var repository = args.Optional("repo")
            ?? await Shell.RunAsync("git", ["rev-parse", "--show-toplevel"], null, ct);

        var name = await Anchors.BuildAsync(repository, args.Optional("commit") ?? "HEAD", args.Optional("label"), kinds,
            args.Optional("root") ?? Anchors.DefaultRoot, args.Has("working-tree"), output, ct);

        output.WriteLine($"Anchor {name} is ready for {string.Join(", ", kinds.Select(Modes.FolderOf))}");
        return 0;
    }
}
