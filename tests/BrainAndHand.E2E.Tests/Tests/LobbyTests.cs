namespace BrainAndHand.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class LobbyTests(WebAppFixture app, PlaywrightFixture playwright)
{
    [Fact]
    public async Task Claiming_and_leaving_a_seat_is_reflected_live_for_every_player_in_the_room()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var hostRoom = await host.Home.CreateRoomAsync();

        await using var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.GotoRoomAsync(hostRoom.Code);

        await hostRoom.ClaimSeatAsync(Side.White, SeatRole.Brain);
        await Expect(guestRoom.SeatOccupantName(Side.White, SeatRole.Brain)).ToContainTextAsync("Alice");

        await hostRoom.LeaveSeatAsync(Side.White, SeatRole.Brain);
        await Expect(guestRoom.SeatJoinButton(Side.White, SeatRole.Brain)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Adding_a_bot_always_starts_it_at_medium_difficulty()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var hostRoom = await host.Home.CreateRoomAsync();

        await using var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.GotoRoomAsync(hostRoom.Code);

        await hostRoom.AddBotAsync(Side.White, SeatRole.Hand);

        await Expect(hostRoom.SeatBotSelect(Side.White, SeatRole.Hand)).ToHaveValueAsync("Medium");
        await Expect(guestRoom.SeatBotSelect(Side.White, SeatRole.Hand)).ToHaveValueAsync("Medium");
    }

    [Fact]
    public async Task Changing_a_bots_difficulty_and_clearing_it_is_reflected_live_for_every_player_in_the_room()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var hostRoom = await host.Home.CreateRoomAsync();

        await using var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.GotoRoomAsync(hostRoom.Code);

        await hostRoom.SetBotAsync(Side.White, SeatRole.Hand, BotDifficulty.Hard);
        await Expect(guestRoom.SeatBotSelect(Side.White, SeatRole.Hand)).ToHaveValueAsync("Hard");

        await hostRoom.ClearBotAsync(Side.White, SeatRole.Hand);
        await Expect(guestRoom.SeatJoinButton(Side.White, SeatRole.Hand)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Host_changing_clock_settings_is_reflected_live_for_every_player_in_the_room()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var hostRoom = await host.Home.CreateRoomAsync();

        await using var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.GotoRoomAsync(hostRoom.Code);

        await Expect(hostRoom.ClockMinutesInput).ToHaveValueAsync("10");
        await Expect(hostRoom.ClockIncrementInput).ToHaveValueAsync("0");
        await Expect(guestRoom.ClockSettingsDisplay).ToHaveTextAsync("10 min + 0 s increment");

        await hostRoom.SetClockSettingsAsync(minutes: 3, incrementSeconds: 2);

        await Expect(guestRoom.ClockSettingsDisplay).ToHaveTextAsync("3 min + 2 s increment");
    }

    [Fact]
    public async Task Start_game_stays_disabled_with_a_hint_until_all_four_seats_are_filled()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var room = await host.Home.CreateRoomAsync();

        await room.ClaimSeatAsync(Side.White, SeatRole.Brain);
        await Expect(room.FillSeatsHint).ToBeVisibleAsync();
        Assert.False(await room.IsStartGameEnabledAsync());

        await room.SetBotAsync(Side.White, SeatRole.Hand, BotDifficulty.Easy);
        await room.SetBotAsync(Side.Black, SeatRole.Brain, BotDifficulty.Easy);
        await room.SetBotAsync(Side.Black, SeatRole.Hand, BotDifficulty.Easy);

        await Expect(room.FillSeatsHint).ToBeHiddenAsync();
        Assert.True(await room.IsStartGameEnabledAsync());
    }

    [Fact]
    public async Task Non_host_players_never_see_a_start_button_and_see_a_waiting_message_once_full()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var hostRoom = await host.Home.CreateRoomAsync();

        await using var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.GotoRoomAsync(hostRoom.Code);

        await hostRoom.ClaimSeatAsync(Side.White, SeatRole.Brain);
        await Expect(guestRoom.StartGameButton).ToBeHiddenAsync();

        await hostRoom.SetBotAsync(Side.White, SeatRole.Hand, BotDifficulty.Easy);
        await hostRoom.SetBotAsync(Side.Black, SeatRole.Brain, BotDifficulty.Easy);
        await hostRoom.SetBotAsync(Side.Black, SeatRole.Hand, BotDifficulty.Easy);

        await Expect(guestRoom.WaitingForHostMessage).ToBeVisibleAsync();
        await Expect(guestRoom.StartGameButton).ToBeHiddenAsync();
    }
}
