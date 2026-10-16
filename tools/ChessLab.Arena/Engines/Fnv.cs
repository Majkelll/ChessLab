using System.Text;

namespace ChessLab.Arena.Engines;

public static class Fnv
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Hash(string text)
    {
        var hash = Offset;
        foreach (var value in Encoding.UTF8.GetBytes(text))
        {
            hash ^= value;
            hash = unchecked(hash * Prime);
        }

        return hash;
    }
}
