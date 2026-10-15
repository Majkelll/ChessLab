using System.Globalization;

namespace ChessLab.Arena.Cli;

public sealed class BenchCompareCommand : IArenaCommand
{
    public string Name => "bench-compare";

    public string Summary => "Compare nodes per second of two bench --output files and fail on a slowdown.";

    public string Usage => "bench-compare --main <file> --candidate <file> [--max-slowdown 10]";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var main = await ReadAsync(args.Required("main"), ct);
        var candidate = await ReadAsync(args.Required("candidate"), ct);
        var maxSlowdown = args.Int("max-slowdown", 10) / 100.0;
        var failed = false;

        foreach (var (mode, (candidateNodes, candidateNps)) in candidate)
        {
            if (!main.TryGetValue(mode, out var baseline))
            {
                output.WriteLine($"{mode}: no measurement on main, skipped");
                continue;
            }

            var change = (candidateNps / baseline.Nps) - 1;
            var slower = change < -maxSlowdown;
            failed |= slower;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{mode}: {candidateNps:0} nodes per second against {baseline.Nps:0} on main ({change:+0.0%;-0.0%}){(slower ? " too slow" : "")}" +
                $"{(candidateNodes != baseline.Nodes ? $", bench changed {baseline.Nodes} -> {candidateNodes}" : "")}"));
        }

        return failed ? 1 : 0;
    }

    private static async Task<Dictionary<string, (long Nodes, double Nps)>> ReadAsync(string path, CancellationToken ct)
    {
        var records = new Dictionary<string, (long, double)>();
        foreach (var line in await File.ReadAllLinesAsync(path, ct))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3)
                continue;

            var (nodes, nps) = (long.Parse(parts[1], CultureInfo.InvariantCulture), double.Parse(parts[2], CultureInfo.InvariantCulture));
            records[parts[0]] = records.TryGetValue(parts[0], out var existing) && existing.Item2 >= nps ? existing : (nodes, nps);
        }

        return records;
    }
}
