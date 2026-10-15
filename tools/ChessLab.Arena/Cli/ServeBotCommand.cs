using System.Text.Json;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;
using ChessLab.Bots;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Cli;

public sealed class ServeBotCommand : IArenaCommand
{
    public string Name => "serve-bot";

    public string Summary => "Answer bot requests on stdin with actions on stdout (used to play anchors).";

    public string Usage =>
        "serve-bot [--seed <n>] [--stockfish <path>] [--kind <mode> --difficulty <level>]  (one BotActionRequest JSON per line)";

    public async Task<int> RunAsync(CommandArgs args, TextWriter output, CancellationToken ct)
    {
        var seed = args.Int("seed", 1);
        var stockfishPath = args.Optional("stockfish") ?? "stockfish";
        var bots = new Dictionary<GameKind, IGameBot>();
        IChessEngineProcess? engine = null;

        try
        {
            if (args.Optional("kind") is { } kindText)
            {
                var kind = Modes.Parse(kindText);
                var difficulty = Enum.Parse<BotDifficulty>(args.Optional("difficulty") ?? nameof(BotDifficulty.Expert), ignoreCase: true);
                if (GameBots.NeedsChessEngine(kind))
                    engine = await StockfishEngine.StartAsync(stockfishPath, ct);

                await BotWarmUp.RunAsync(kind, difficulty, engine, ct);
                await output.WriteLineAsync(BotWarmUp.ReadyLine);
                await output.FlushAsync(ct);
            }

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
