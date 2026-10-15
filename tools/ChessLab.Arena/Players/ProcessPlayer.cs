using System.Diagnostics;
using System.Text.Json;
using ChessLab.Bots;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Players;

public sealed class AnchorPlayerFactory(string directory, BotDifficulty difficulty, ArenaSettings settings) : IPlayerFactory
{
    public string Name => $"anchor:{Path.GetFileName(Path.TrimEndingDirectorySeparator(directory))}:{Difficulty}";

    public BotDifficulty Difficulty => difficulty;

    public TimeSpan BudgetFor(GameKind kind) =>
        new BotPlayerFactory(difficulty, settings).BudgetFor(kind);

    public Task<IArenaPlayer> CreateAsync(GameKind kind, int seed, CancellationToken ct)
    {
        var executable = Path.Combine(directory, "ChessLab.Arena");
        var start = new ProcessStartInfo(File.Exists(executable) ? executable : "dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        if (!File.Exists(executable))
            start.ArgumentList.Add(Path.Combine(directory, "ChessLab.Arena.dll"));

        foreach (var argument in (string[])["serve-bot", "--seed", seed.ToString(), "--stockfish", settings.StockfishPath])
            start.ArgumentList.Add(argument);

        var process = Process.Start(start)
            ?? throw new InvalidOperationException($"The anchor in {directory} did not start.");
        return Task.FromResult<IArenaPlayer>(new ProcessPlayer(process));
    }

    private sealed class ProcessPlayer(Process process) : IArenaPlayer
    {
        public async Task<GameAction> ChooseAsync(BotActionRequest request, CancellationToken ct)
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, BotServiceJson.Options));
            await process.StandardInput.FlushAsync(ct);

            var line = await process.StandardOutput.ReadLineAsync(ct)
                ?? throw new IOException("The anchor process ended without answering.");

            return JsonSerializer.Deserialize<GameAction>(line, BotServiceJson.Options)
                ?? throw new InvalidOperationException("The anchor answered with an empty action.");
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                process.StandardInput.Close();
                if (!process.WaitForExit(TimeSpan.FromSeconds(5)))
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            process.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
