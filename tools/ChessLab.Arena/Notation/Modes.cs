using System.Text;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Notation;

public static class Modes
{
    public static IReadOnlyList<GameKind> All { get; } = Enum.GetValues<GameKind>();

    public static string FolderOf(GameKind kind)
    {
        var name = kind.ToString();
        var folder = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
                folder.Append('-');

            folder.Append(char.ToLowerInvariant(name[i]));
        }

        return folder.ToString();
    }

    public static GameKind Parse(string text)
    {
        foreach (var kind in All)
        {
            if (string.Equals(FolderOf(kind), text, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind.ToString(), text, StringComparison.OrdinalIgnoreCase))
            {
                return kind;
            }
        }

        throw new ArgumentException(
            $"Unknown mode '{text}'. Use one of: {string.Join(", ", All.Select(FolderOf))}.");
    }

    public static bool IsReplayable(GameKind kind) =>
        kind is GameKind.HandAndBrain or GameKind.ProgressiveChess or GameKind.AliceChess
            or GameKind.AbsorptionChess or GameKind.MartianChess;
}
