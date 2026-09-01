using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using ChessLab.E2E.Tests.PageObjects;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.Helpers;

/// <summary>Every human seat's browser session plus its board once a Card Chess game has started.</summary>
public sealed record StartedCardChessGame(
    string Code,
    IReadOnlyDictionary<Side, CardChessGamePage> Games,
    IReadOnlyList<TestPlayer> Players)
{
    public CardChessGamePage this[Side side] => Games[side];
}

/// <summary>Fluent "N humans (+ M bots) in a Card Chess room, then start the game" setup — the Card
/// Chess sibling of <see cref="GameRoomBuilder"/>.</summary>
public sealed class CardChessRoomBuilder(IBrowser browser, string baseUrl)
{
    private readonly List<(Side Side, string Name)> humanSeats = [];
    private readonly List<(Side Side, BotDifficulty Difficulty)> botSeats = [];
    private (int Minutes, int IncrementSeconds)? clockSettings;

    /// <summary>The first human added becomes the room host (creates the room, starts the game).</summary>
    public CardChessRoomBuilder WithHuman(Side side, string name)
    {
        humanSeats.Add((side, name));
        return this;
    }

    public CardChessRoomBuilder WithBot(Side side, BotDifficulty difficulty)
    {
        botSeats.Add((side, difficulty));
        return this;
    }

    public CardChessRoomBuilder WithClock(int minutes, int incrementSeconds)
    {
        clockSettings = (minutes, incrementSeconds);
        return this;
    }

    public async Task<StartedCardChessGame> StartAsync()
    {
        if (humanSeats.Count == 0)
            throw new InvalidOperationException("A room needs at least one human to act as host.");

        var seated = new List<(Side Side, TestPlayer Player, RoomPage Room)>();

        var (hostSide, hostName) = humanSeats[0];
        var host = await TestPlayer.SignInAsync(browser, baseUrl, hostName);
        var hostRoom = await host.Home.CreateCardChessRoomAsync();
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

        var games = new Dictionary<Side, CardChessGamePage>();
        foreach (var (side, _, room) in seated)
        {
            games[side] = room == hostRoom
                ? await room.StartCardChessGameAsync()
                : await room.WaitForCardChessGameStartedAsync();
        }

        return new StartedCardChessGame(hostRoom.Code, games, seated.Select(s => s.Player).ToArray());
    }
}
