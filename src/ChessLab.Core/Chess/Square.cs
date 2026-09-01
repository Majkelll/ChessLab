using System.Text.Json.Serialization;

namespace ChessLab.Core.Chess;

/// <summary>A board square. File/Rank are 0-based (a1 = 0,0; h8 = 7,7).</summary>
public readonly record struct Square
{
    public int File { get; }
    public int Rank { get; }

    // Without this, System.Text.Json's constructor-selection heuristic silently prefers the
    // implicit parameterless struct constructor over this one (since File/Rank have no public
    // setters for it to fall back to), deserializing every Square as default — (0,0) i.e. "a1" —
    // with no error. [JsonConstructor] pins down the one to use.
    [JsonConstructor]
    public Square(int file, int rank)
    {
        if (file is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(file));
        if (rank is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(rank));
        File = file;
        Rank = rank;
    }

    public static Square Parse(string algebraic)
    {
        if (algebraic.Length != 2)
            throw new FormatException($"Invalid square notation: '{algebraic}'.");

        var file = char.ToLowerInvariant(algebraic[0]) - 'a';
        var rank = algebraic[1] - '1';
        return new Square(file, rank);
    }

    public override string ToString() => $"{(char)('a' + File)}{Rank + 1}";
}
