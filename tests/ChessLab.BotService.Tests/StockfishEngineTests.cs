using System.Diagnostics;
using ChessLab.Bots;

namespace ChessLab.BotService.Tests;

public class StockfishEngineTests
{
    private static readonly string? Stockfish = FindStockfish();

    private static string? FindStockfish()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("stockfish")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });

            if (process is null)
                return null;

            process.Kill(entireProcessTree: true);
            return "stockfish";
        }
        catch
        {
            return null;
        }
    }

    [SkippableFact]
    public async Task Stockfish_searches_only_among_the_candidate_moves()
    {
        Skip.If(Stockfish is null, "No `stockfish` binary found on PATH — install it to run engine tests.");

        await using var engine = await StockfishEngine.StartAsync(Stockfish!);
        var (best, _) = await engine.GoAsync(
            "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", ["a2a3", "h2h3"], elo: 1320, movetimeMs: 100);

        Assert.Contains(best, new[] { "a2a3", "h2h3" });
        Assert.False(engine.HasExited);
    }

    [SkippableFact]
    public async Task Stockfish_reports_that_it_exited_after_disposal()
    {
        Skip.If(Stockfish is null, "No `stockfish` binary found on PATH — install it to run engine tests.");

        var engine = await StockfishEngine.StartAsync(Stockfish!);
        await engine.DisposeAsync();

        Assert.True(engine.HasExited);
    }

    [Fact]
    public async Task Starting_a_missing_engine_binary_throws() =>
        await Assert.ThrowsAnyAsync<Exception>(() => StockfishEngine.StartAsync("/nonexistent/stockfish-binary"));
}
