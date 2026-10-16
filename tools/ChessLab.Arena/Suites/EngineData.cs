using ChessLab.Arena.Notation;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Suites;

public sealed class EngineData(string root)
{
    public const string DefaultRelativePath = "tests/engine-data";

    public string Root { get; } = root;

    public static EngineData Locate(string? explicitRoot)
    {
        if (explicitRoot is not null)
            return new EngineData(Path.GetFullPath(explicitRoot));

        for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, DefaultRelativePath);
            if (Directory.Exists(candidate))
                return new EngineData(candidate);
        }

        throw new DirectoryNotFoundException(
            $"Could not find {DefaultRelativePath} above {Environment.CurrentDirectory}; pass --data.");
    }

    public string ModeDirectory(GameKind kind) => Path.Combine(Root, Modes.FolderOf(kind));

    public string FileOf(GameKind kind, string name) => Path.Combine(ModeDirectory(kind), name);

    public IReadOnlyList<PositionLine> Openings(GameKind kind) =>
        [.. Lines(kind, "openings.txt").Select(PositionLine.Parse)];

    public IReadOnlyList<string> Lines(GameKind kind, string name)
    {
        var path = FileOf(kind, name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"{path} does not exist.", path);

        return [.. File.ReadAllLines(path).Select(line => line.Trim()).Where(line => line.Length > 0)];
    }
}
