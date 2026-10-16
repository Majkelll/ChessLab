using System.Diagnostics;

namespace ChessLab.E2E.Tests.Infrastructure;

internal static class StockfishLocator
{
    public static string? Find()
    {
        var candidates = new List<string> { "stockfish" };

        var path = Environment.GetEnvironmentVariable("PATH");
        if (path is not null)
        {
            foreach (var dir in path.Split(Path.PathSeparator))
            {
                var full = Path.Combine(dir, "stockfish");
                if (File.Exists(full))
                    candidates.Add(full);
            }
        }

        foreach (var candidate in candidates.Distinct())
        {
            if (CanStart(candidate))
                return candidate;
        }

        return null;
    }

    private static bool CanStart(string executable)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null)
                return false;

            process.Kill(entireProcessTree: true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
