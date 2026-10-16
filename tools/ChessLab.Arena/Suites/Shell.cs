using System.Diagnostics;

namespace ChessLab.Arena.Suites;

public static class Shell
{
    public static async Task<string> RunAsync(string program, IEnumerable<string> arguments, string? workingDirectory,
        CancellationToken ct)
    {
        var start = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException($"{program} did not start.");
        var output = process.StandardOutput.ReadToEndAsync(ct);
        var errors = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{program} {string.Join(' ', start.ArgumentList)} failed with exit code {process.ExitCode}: {await errors}");

        return (await output).Trim();
    }
}
