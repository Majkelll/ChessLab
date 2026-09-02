using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using ChessLab.E2E.Tests.PageObjects;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.Helpers;

public sealed record StartedArcaneChessGame(
    string Code,
    IReadOnlyDictionary<Side, ArcaneChessGamePage> Games,
    IReadOnlyList<TestPlayer> Players)
{
    public ArcaneChessGamePage this[Side side] => Games[side];
}

public sealed class ArcaneChessRoomBuilder(IBrowser browser, string baseUrl)
{
    private readonly List<(Side Side, string Name)> humanSeats = [];

    public ArcaneChessRoomBuilder WithHuman(Side side, string name)
    {
        humanSeats.Add((side, name));
        return this;
    }

    public async Task<StartedArcaneChessGame> StartAsync()
    {
        if (humanSeats.Count == 0)
            throw new InvalidOperationException("A room needs at least one human to act as host.");

        var seated = new List<(Side Side, TestPlayer Player, RoomPage Room)>();

        var (hostSide, hostName) = humanSeats[0];
        var host = await TestPlayer.SignInAsync(browser, baseUrl, hostName);
        var hostRoom = await host.Home.CreateArcaneChessRoomAsync();
        await hostRoom.ClaimSeatAsync(hostSide, SeatRole.Player);
        seated.Add((hostSide, host, hostRoom));

        foreach (var (side, name) in humanSeats.Skip(1))
        {
            var player = await TestPlayer.SignInAsync(browser, baseUrl, name);
            var room = await player.GotoRoomAsync(hostRoom.Code);
            await room.ClaimSeatAsync(side, SeatRole.Player);
            seated.Add((side, player, room));
        }

        var games = new Dictionary<Side, ArcaneChessGamePage>();
        foreach (var (side, _, room) in seated)
        {
            games[side] = room == hostRoom
                ? await room.StartArcaneChessGameAsync()
                : await room.WaitForArcaneChessGameStartedAsync();
        }

        return new StartedArcaneChessGame(hostRoom.Code, games, seated.Select(s => s.Player).ToArray());
    }
}
