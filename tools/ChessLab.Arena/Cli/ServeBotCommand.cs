using System.Text.Json;
using ChessLab.Bots;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Cli;

public sealed class ServeBotCommand : IArenaCommand
{
    public string Name => "serve-bot";

    public string Summary => "Answer bot requests on stdin with actions on stdout (used to play anchors).";

    public string Usage => "serve-bot [--seed <n>] [--stockfish <path>]  (one BotActionRequest JSON per line)";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var seed = args.Int("seed", 1);
        var stockfishPath = args.Optional("stockfish") ?? "stockfish";
        var bots = new Dictionary<GameKind, IGameBot>();
        IChessEngineProcess? engine = null;

        try
        {
            while (await Console.In.ReadLineAsync(ct) is { } line)
            {
                var request = JsonSerializer.Deserialize<BotActionRequest>(line, BotServiceJson.Options)
                    ?? throw new FormatException("Empty request.");

                if (!bots.TryGetValue(request.Kind, out var bot))
                {
                    if (GameBots.NeedsChessEngine(request.Kind))
                        engine ??= await StockfishEngine.StartAsync(stockfishPath, ct);

                    bot = bots[request.Kind] = GameBots.For(request.Kind, engine, seed);
                }

                var action = await bot.ChooseActionAsync(request.State, request.Seat, request.Difficulty, ct);
                await output.WriteLineAsync(JsonSerializer.Serialize(action, BotServiceJson.Options));
                await output.FlushAsync(ct);
            }
        }
        finally
        {
            if (engine is not null)
                await engine.DisposeAsync();
        }

        return 0;
    }
}
