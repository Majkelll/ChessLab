namespace BrainAndHand.Core.Chess;

/// <summary>A board square. File/Rank are 0-based (a1 = 0,0; h8 = 7,7).</summary>
public readonly record struct Square
{
    public int File { get; }
    public int Rank { get; }

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
