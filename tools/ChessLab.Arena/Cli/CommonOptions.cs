using ChessLab.Arena.Matches;
using ChessLab.Arena.Players;
using ChessLab.Arena.Suites;

namespace ChessLab.Arena.Cli;

public static class CommonOptions
{
    public const string Usage =
        "[--data <dir>] [--stockfish <path>] [--budget-ms <n>] [--concurrency <n>] [--seed <n>] [--yield-to <queue url>]";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    public static Func<CancellationToken, Task>? BeforePair(CommandArgs args, TextWriter output) =>
        args.Optional("yield-to") is { } url
            ? new BotQueueGate(Http, new Uri(url), BotQueueGate.DefaultPollInterval, output).WaitUntilIdleAsync
            : null;

    public static ArenaSettings Settings(CommandArgs args) => new(
        args.Optional("stockfish") ?? "stockfish",
        args.Optional("budget-ms") is { } budget ? TimeSpan.FromMilliseconds(int.Parse(budget)) : null);

    public static EngineData Data(CommandArgs args) => EngineData.Locate(args.Optional("data"));

    public static int Concurrency(CommandArgs args) => args.Int("concurrency", Math.Max(1, Environment.ProcessorCount - 1));
}
