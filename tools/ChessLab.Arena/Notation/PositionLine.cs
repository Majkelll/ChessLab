namespace ChessLab.Arena.Notation;

public sealed record PositionLine(int Seed, IReadOnlyList<string> Actions)
{
    private const string SeedPrefix = "seed=";

    public static PositionLine Start(int seed = 0) => new(seed, []);

    public static PositionLine Parse(string text) => Parse(text.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static PositionLine Parse(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0 || !tokens[0].StartsWith(SeedPrefix, StringComparison.Ordinal))
            return new PositionLine(0, [.. tokens]);

        if (!int.TryParse(tokens[0][SeedPrefix.Length..], out var seed))
            throw new FormatException($"'{tokens[0]}' is not a seed.");

        return new PositionLine(seed, [.. tokens.Skip(1)]);
    }

    public PositionLine Then(string action) => this with { Actions = [.. Actions, action] };

    public override string ToString() =>
        Actions.Count == 0 ? $"{SeedPrefix}{Seed}" : $"{SeedPrefix}{Seed} {string.Join(' ', Actions)}";
}
