using System.Collections.Concurrent;
using System.Text.Json;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Bots;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Players;

public static class BotWarmUp
{
    public const string ReadyLine = "ready";

    private const int WholeGameActions = 300;

    private static readonly ConcurrentDictionary<(GameKind, BotDifficulty), Lazy<Task>> InThisProcess = new();

    public static Task OnceAsync(GameKind kind, BotDifficulty difficulty, IChessEngine? engine, CancellationToken ct) =>
        InThisProcess.GetOrAdd((kind, difficulty), _ => new Lazy<Task>(() => RunAsync(kind, difficulty, engine, ct))).Value;

    public static async Task RunAsync(GameKind kind, BotDifficulty difficulty, IChessEngine? engine, CancellationToken ct)
    {
        var session = CoreGames.Start(kind, PositionLine.Start());
        var bot = GameBots.For(kind, engine, seed: 0);
        var actions = GameBots.NeedsChessEngine(kind) ? 1 : WholeGameActions;

        for (var played = 0; played < actions && session.HasActiveGame; played++)
        {
            var seat = session.ActiveSeats.First();
            var request = RoundTrip(new BotActionRequest(kind, seat, difficulty, session.ToStateDto(seat.Side)));
            var action = RoundTrip(await bot.ChooseActionAsync(request.State, request.Seat, request.Difficulty, ct));
            CoreGames.Apply(session, new SeatAction(seat, action));
        }
    }

    public static void WarmUpRequests(GameKind kind, BotDifficulty difficulty)
    {
        var session = CoreGames.Start(kind, PositionLine.Start());
        var seat = session.ActiveSeats.First();
        RoundTrip(new BotActionRequest(kind, seat, difficulty, session.ToStateDto(seat.Side)));
        RoundTrip(CoreGames.LegalActions(session)[0].Action);
    }

    private static T RoundTrip<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, BotServiceJson.Options), BotServiceJson.Options)
        ?? throw new InvalidOperationException($"{typeof(T).Name} did not survive a JSON round trip.");
}
