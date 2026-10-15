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

    private static readonly ConcurrentDictionary<(GameKind, BotDifficulty), Lazy<Task>> InThisProcess = new();

    public static Task OnceAsync(GameKind kind, BotDifficulty difficulty, IChessEngine? engine, CancellationToken ct) =>
        InThisProcess.GetOrAdd((kind, difficulty), _ => new Lazy<Task>(() => RunAsync(kind, difficulty, engine, ct))).Value;

    public static async Task RunAsync(GameKind kind, BotDifficulty difficulty, IChessEngine? engine, CancellationToken ct)
    {
        var request = StartRequest(kind, difficulty, out _);
        var action = await GameBots.For(kind, engine, seed: 0)
            .ChooseActionAsync(request.State, request.Seat, request.Difficulty, ct);
        RoundTrip(request, action);
    }

    public static void WarmUpRequests(GameKind kind, BotDifficulty difficulty)
    {
        var request = StartRequest(kind, difficulty, out var session);
        RoundTrip(request, CoreGames.LegalActions(session)[0].Action);
    }

    private static BotActionRequest StartRequest(GameKind kind, BotDifficulty difficulty, out IRoomSession session)
    {
        session = CoreGames.Start(kind, PositionLine.Start());
        var seat = session.ActiveSeats.First();
        return new BotActionRequest(kind, seat, difficulty, session.ToStateDto(seat.Side));
    }

    private static void RoundTrip(BotActionRequest request, GameAction action)
    {
        JsonSerializer.Deserialize<BotActionRequest>(JsonSerializer.Serialize(request, BotServiceJson.Options),
            BotServiceJson.Options);
        JsonSerializer.Deserialize<GameAction>(JsonSerializer.Serialize(action, BotServiceJson.Options),
            BotServiceJson.Options);
    }
}
