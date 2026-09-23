using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using ChessLab.E2E.Tests.PageObjects;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.Helpers;

public sealed record StartedVariantGame(
    string Code,
    IReadOnlyDictionary<Side, VariantGamePage> Games,
    IReadOnlyList<TestPlayer> Players)
{
    public VariantGamePage this[Side side] => Games[side];
}

/// <summary>Seats and starts a room for any of the six modes that share the board shell — the only
/// thing that changes between them is which card on the home page opens the room and which route
/// the game lives at.</summary>
public sealed class VariantRoomBuilder(IBrowser browser, string baseUrl, GameKind kind)
{
    private readonly List<(Side Side, string Name)> humanSeats = [];
    private readonly List<(Side Side, BotDifficulty Difficulty)> botSeats = [];
    private (int Minutes, int IncrementSeconds)? clockSettings;

    public VariantRoomBuilder WithHuman(Side side, string name)
    {
        humanSeats.Add((side, name));
        return this;
    }

    public VariantRoomBuilder WithBot(Side side, BotDifficulty difficulty)
    {
        botSeats.Add((side, difficulty));
        return this;
    }

    public VariantRoomBuilder WithClock(int minutes, int incrementSeconds)
    {
        clockSettings = (minutes, incrementSeconds);
        return this;
    }

    public async Task<StartedVariantGame> StartAsync()
    {
        if (humanSeats.Count == 0)
            throw new InvalidOperationException("A room needs at least one human to act as host.");

        var seated = new List<(Side Side, TestPlayer Player, RoomPage Room)>();

        var (hostSide, hostName) = humanSeats[0];
        var host = await TestPlayer.SignInAsync(browser, baseUrl, hostName);
        var hostRoom = await host.Home.CreateVariantRoomAsync(kind);
        await hostRoom.ClaimSeatAsync(hostSide, SeatRole.Player);
        seated.Add((hostSide, host, hostRoom));

        foreach (var (side, name) in humanSeats.Skip(1))
        {
            var player = await TestPlayer.SignInAsync(browser, baseUrl, name);
            var room = await player.GotoRoomAsync(hostRoom.Code);
            await room.ClaimSeatAsync(side, SeatRole.Player);
            seated.Add((side, player, room));
        }

        foreach (var (side, difficulty) in botSeats)
            await hostRoom.SetBotAsync(side, SeatRole.Player, difficulty);

        if (clockSettings is { } clock)
            await hostRoom.SetClockSettingsAsync(clock.Minutes, clock.IncrementSeconds);

        var games = new Dictionary<Side, VariantGamePage>();
        foreach (var (side, _, room) in seated)
        {
            games[side] = room == hostRoom
                ? await room.StartVariantGameAsync(kind)
                : await room.WaitForVariantGameStartedAsync(kind);
        }

        return new StartedVariantGame(hostRoom.Code, games, seated.Select(s => s.Player).ToArray());
    }
}

public static class VariantRoutes
{
    public static string Of(GameKind kind) => kind switch
    {
        GameKind.BiddingChess => "bidding",
        GameKind.ProgressiveChess => "progressive",
        GameKind.AliceChess => "alice",
        GameKind.AbsorptionChess => "absorption",
        GameKind.MartianChess => "martian",
        GameKind.DraftChess => "draft",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "That mode has its own page object."),
    };

    public static string CreateButtonTestId(GameKind kind) =>
        $"create-{kind.ToString().ToLowerInvariant()}-room-btn";
}
