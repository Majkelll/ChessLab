using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using ChessLab.E2E.Tests.PageObjects;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.Helpers;

public sealed record StartedGame(
    string Code,
    IReadOnlyDictionary<(Side Side, SeatRole Role), GamePage> Games,
    IReadOnlyList<TestPlayer> Players)
{
    public GamePage this[Side side, SeatRole role] => Games[(side, role)];
}

public sealed class GameRoomBuilder(IBrowser browser, string baseUrl)
{
    private readonly List<(Side Side, SeatRole Role, string Name)> humanSeats = [];
    private readonly List<(Side Side, SeatRole Role, BotDifficulty Difficulty)> botSeats = [];
    private (int Minutes, int IncrementSeconds)? clockSettings;

    public GameRoomBuilder WithHuman(Side side, SeatRole role, string name)
    {
        humanSeats.Add((side, role, name));
        return this;
    }

    public GameRoomBuilder WithBot(Side side, SeatRole role, BotDifficulty difficulty)
    {
        botSeats.Add((side, role, difficulty));
        return this;
    }

    public GameRoomBuilder WithClock(int minutes, int incrementSeconds)
    {
        clockSettings = (minutes, incrementSeconds);
        return this;
    }

    public async Task<StartedGame> StartAsync()
    {
        if (humanSeats.Count == 0)
            throw new InvalidOperationException("A room needs at least one human to act as host.");

        var seated = new List<(Side Side, SeatRole Role, TestPlayer Player, RoomPage Room)>();

        var (hostSide, hostRole, hostName) = humanSeats[0];
        var host = await TestPlayer.SignInAsync(browser, baseUrl, hostName);
        var hostRoom = await host.Home.CreateRoomAsync();
        await hostRoom.ClaimSeatAsync(hostSide, hostRole);
        seated.Add((hostSide, hostRole, host, hostRoom));

        foreach (var (side, role, name) in humanSeats.Skip(1))
        {
            var player = await TestPlayer.SignInAsync(browser, baseUrl, name);
            var room = await player.GotoRoomAsync(hostRoom.Code);
            await room.ClaimSeatAsync(side, role);
            seated.Add((side, role, player, room));
        }

        foreach (var (side, role, difficulty) in botSeats)
            await hostRoom.SetBotAsync(side, role, difficulty);

        if (clockSettings is { } clock)
            await hostRoom.SetClockSettingsAsync(clock.Minutes, clock.IncrementSeconds);

        var games = new Dictionary<(Side, SeatRole), GamePage>();
        foreach (var (side, role, _, room) in seated)
        {
            games[(side, role)] = room == hostRoom
                ? await room.StartGameAsync()
                : await room.WaitForGameStartedAsync();
        }

        return new StartedGame(hostRoom.Code, games, seated.Select(s => s.Player).ToArray());
    }
}
