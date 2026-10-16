using ChessLab.Arena.Notation;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Suites;

public static class Anchors
{
    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "chesslab-arena");

    public static string DirectoryOf(string root, GameKind kind, string label) =>
        Path.Combine(root, "anchors", Modes.FolderOf(kind), label);

    public static IReadOnlyList<string> Existing(string root, GameKind kind)
    {
        var directory = Path.Combine(root, "anchors", Modes.FolderOf(kind));
        return Directory.Exists(directory)
            ? [.. Directory.GetDirectories(directory).Order(StringComparer.Ordinal)]
            : [];
    }

    public static async Task<string> BuildAsync(string repository, string commit, string? label,
        IReadOnlyList<GameKind> kinds, string root, bool fromWorkingTree, TextWriter log, CancellationToken ct)
    {
        var hash = await Shell.RunAsync("git", ["rev-parse", "--short=12", commit], repository, ct);
        var name = label ?? (fromWorkingTree ? $"{hash}-worktree" : hash);
        var source = fromWorkingTree ? repository : Path.Combine(Path.GetTempPath(), $"chesslab-anchor-{name}");

        if (!fromWorkingTree)
            await Shell.RunAsync("git", ["worktree", "add", "--detach", source, hash], repository, ct);

        try
        {
            var first = DirectoryOf(root, kinds[0], name);
            log.WriteLine($"Publishing the arena at {name} to {first}");
            await Shell.RunAsync("dotnet",
                ["publish", Path.Combine(source, "tools", "ChessLab.Arena"), "-c", "Release", "-o", first], source, ct);

            foreach (var kind in kinds.Skip(1))
                CopyDirectory(first, DirectoryOf(root, kind, name));
        }
        finally
        {
            if (!fromWorkingTree)
                await Shell.RunAsync("git", ["worktree", "remove", "--force", source], repository, ct);
        }

        return name;
    }

    private static void CopyDirectory(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
