using System.Text.Json.Serialization;

namespace ChessLab.Core.Chess;

public readonly record struct Square
{
    public int File { get; }
    public int Rank { get; }

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
