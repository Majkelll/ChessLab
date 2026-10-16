using System.Globalization;
using ChessLab.Arena.Notation;

namespace ChessLab.Arena.Checks;

public sealed record PerftReference(int Depth, long Nodes, PositionLine Position)
{
    public static PerftReference Parse(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 3)
            throw new FormatException($"'{line}' is not a perft line: expected depth, nodes and a position.");

        return new PerftReference(int.Parse(tokens[0], CultureInfo.InvariantCulture),
            long.Parse(tokens[1], CultureInfo.InvariantCulture), PositionLine.Parse(tokens[2..]));
    }

    public override string ToString() => $"{Depth} {Nodes} {Position}";
}
