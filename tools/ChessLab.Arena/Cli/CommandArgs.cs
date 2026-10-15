using System.Globalization;

namespace ChessLab.Arena.Cli;

public sealed class CommandArgs
{
    private readonly Dictionary<string, string> options = new(StringComparer.Ordinal);
    private readonly HashSet<string> flags = new(StringComparer.Ordinal);

    public CommandArgs(IEnumerable<string> arguments)
    {
        var list = arguments.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            var argument = list[i];
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                Positional.Add(argument);
                continue;
            }

            var name = argument[2..];
            if (i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal))
                options[name] = list[++i];
            else
                flags.Add(name);
        }
    }

    public List<string> Positional { get; } = [];

    public bool Has(string name) => flags.Contains(name) || options.ContainsKey(name);

    public string? Optional(string name) => options.GetValueOrDefault(name);

    public string Required(string name) =>
        options.TryGetValue(name, out var value) ? value : throw new ArgumentException($"--{name} is required.");

    public int Int(string name, int fallback) =>
        options.TryGetValue(name, out var value) ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;

    public (double Low, double High) Bounds(string name, (double, double) fallback)
    {
        if (!options.TryGetValue(name, out var value))
            return fallback;

        var parts = value.Trim('[', ']').Split(',');
        if (parts.Length != 2)
            throw new ArgumentException($"--{name} takes two numbers, like 0,5.");

        return (double.Parse(parts[0], CultureInfo.InvariantCulture), double.Parse(parts[1], CultureInfo.InvariantCulture));
    }
}
