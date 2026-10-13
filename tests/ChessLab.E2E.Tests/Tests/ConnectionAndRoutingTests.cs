namespace ChessLab.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class ConnectionAndRoutingTests(WebAppFixture app, PlaywrightFixture playwright)
{
    private const string TrackWebSockets = """
        window.openedSockets = [];
        window.WebSocket = class extends window.WebSocket {
            constructor(...args) {
                super(...args);
                window.openedSockets.push(this);
            }
        };
        """;

    [Fact]
    public async Task A_lobby_keeps_receiving_changes_after_its_hub_connection_dropped_and_came_back()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var hostRoom = await host.Home.CreateRoomAsync();

        await using var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        await guest.Context.AddInitScriptAsync(TrackWebSockets);
        var guestRoom = await guest.GotoRoomAsync(hostRoom.Code);
        await Expect(guestRoom.SeatJoinButton(Side.White, SeatRole.Brain)).ToBeVisibleAsync();

        await guest.Page.EvaluateAsync("window.openedSockets.at(-1).close()");
        await hostRoom.ClaimSeatAsync(Side.White, SeatRole.Brain);

        await Expect(guestRoom.SeatOccupantName(Side.White, SeatRole.Brain))
            .ToContainTextAsync("Alice", new() { Timeout = 30_000 });
        Assert.True(await guest.Page.EvaluateAsync<int>("window.openedSockets.length") >= 2);
    }

    [Fact]
    public async Task Opening_a_game_url_before_the_game_started_leads_to_the_lobby()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var room = await host.Home.CreateRoomAsync();

        await host.Page.GotoAsync($"{app.BaseUrl}/game/{room.Code}");

        await host.Page.WaitForURLAsync($"{app.BaseUrl}/room/{room.Code}");
    }

    [Fact]
    public async Task Opening_another_modes_url_for_a_running_game_leads_to_the_right_page()
    {
        var game = await new CardChessRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, "Alice")
            .WithHuman(Side.Black, "Bob")
            .StartAsync();

        await game[Side.White].Page.GotoAsync($"{app.BaseUrl}/alice/{game.Code}");

        await game[Side.White].Page.WaitForURLAsync($"{app.BaseUrl}/cardchess/{game.Code}");
    }

    [Fact]
    public async Task Arcane_Chess_hides_the_opponents_hand_and_Card_Chess_shows_it()
    {
        var arcane = await new ArcaneChessRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, "Alice")
            .WithHuman(Side.Black, "Bob")
            .StartAsync();
        var cards = await new CardChessRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, "Cezary")
            .WithHuman(Side.Black, "Dorota")
            .StartAsync();

        await Expect(arcane[Side.White].Page.GetByTestId("opponent-hand-hidden")).ToBeVisibleAsync();
        await Expect(arcane[Side.Black].Page.GetByTestId("opponent-hand-hidden")).ToBeVisibleAsync();
        await Expect(cards[Side.White].Page.GetByTestId("opponent-hand").Locator(".hand-row > *")).ToHaveCountAsync(5);
    }

    [Fact]
    public async Task A_guest_sees_no_bot_controls_in_the_lobby()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var hostRoom = await host.Home.CreateRoomAsync();
        await using var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.GotoRoomAsync(hostRoom.Code);

        await hostRoom.AddBotAsync(Side.White, SeatRole.Hand);

        await Expect(guestRoom.SeatBotSelect(Side.White, SeatRole.Hand)).ToBeDisabledAsync();
        await Expect(guestRoom.SeatRemoveButton(Side.White, SeatRole.Hand)).ToHaveCountAsync(0);
        await Expect(guestRoom.SeatAddBotButton(Side.White, SeatRole.Brain)).ToHaveCountAsync(0);
        await Expect(hostRoom.SeatAddBotButton(Side.White, SeatRole.Brain)).ToBeVisibleAsync();
    }
}
