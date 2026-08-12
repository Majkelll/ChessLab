using System.Diagnostics;

namespace BrainAndHand.Bots;

/// <summary>A single Stockfish process driven over UCI. Requests are serialized — one engine handles one game at a time.</summary>
public sealed class StockfishEngine : IAsyncDisposable
{
    private readonly Process process;
    private readonly SemaphoreSlim gate = new(1, 1);
    private int? currentSkillLevel;

    private StockfishEngine(Process process) => this.process = process;

    public static async Task<StockfishEngine> StartAsync(string executablePath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo(executablePath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start Stockfish at '{executablePath}'.");

        var engine = new StockfishEngine(process);
        await engine.WriteLineAsync("uci");
        await engine.ReadUntilAsync(line => line == "uciok", ct);
        await engine.WriteLineAsync("isready");
        await engine.ReadUntilAsync(line => line == "readyok", ct);

        return engine;
    }

    /// <summary>Searches only among <paramref name="searchMovesUci"/> and returns the best one plus its evaluation
    /// (centipawns from the side-to-move's perspective; mate scores are mapped to large magnitudes).</summary>
    public async Task<(string BestMoveUci, int? ScoreCentipawns)> GoAsync(
        string fen, IReadOnlyList<string> searchMovesUci, int skillLevel, int movetimeMs, CancellationToken ct = default)
    {
        if (searchMovesUci.Count == 0)
            throw new ArgumentException("At least one candidate move is required.", nameof(searchMovesUci));

        await gate.WaitAsync(ct);
        try
        {
            if (currentSkillLevel != skillLevel)
            {
                await WriteLineAsync($"setoption name Skill Level value {skillLevel}");
                currentSkillLevel = skillLevel;
            }

            await WriteLineAsync($"position fen {fen}");
            await WriteLineAsync($"go movetime {movetimeMs} searchmoves {string.Join(' ', searchMovesUci)}");

            int? lastScore = null;
            while (true)
            {
                var line = await ReadLineAsync(ct);
                if (line is null)
                    throw new IOException("Stockfish process ended unexpectedly.");

                if (line.StartsWith("info ", StringComparison.Ordinal) && line.Contains(" score "))
                    lastScore = ParseScore(line) ?? lastScore;
                else if (line.StartsWith("bestmove ", StringComparison.Ordinal))
                    return (line.Split(' ')[1], lastScore);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private static int? ParseScore(string infoLine)
    {
        var tokens = infoLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < tokens.Length - 2; i++)
        {
            if (tokens[i] != "score")
                continue;

            if (!int.TryParse(tokens[i + 2], out var value))
                return null;

            return tokens[i + 1] switch
            {
                "cp" => value,
                "mate" => value > 0 ? 100_000 - value : -100_000 - value,
                _ => null,
            };
        }
        return null;
    }

    private async Task ReadUntilAsync(Func<string, bool> predicate, CancellationToken ct)
    {
        while (true)
        {
            var line = await ReadLineAsync(ct);
            if (line is null)
                throw new IOException("Stockfish process ended unexpectedly.");
            if (predicate(line))
                return;
        }
    }

    private Task<string?> ReadLineAsync(CancellationToken ct) => process.StandardOutput.ReadLineAsync(ct).AsTask();

    private async Task WriteLineAsync(string command)
    {
        await process.StandardInput.WriteLineAsync(command);
        await process.StandardInput.FlushAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await WriteLineAsync("quit");
            await process.WaitForExitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(2)).Token);
        }
        catch
        {
            // Process may already be gone, or ignored quit — fall through to a hard kill.
        }
        finally
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            process.Dispose();
            gate.Dispose();
        }
    }
}
