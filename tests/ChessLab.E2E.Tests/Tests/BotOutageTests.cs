using System.Text.RegularExpressions;

namespace ChessLab.E2E.Tests.Tests;

[Collection(BotOutageCollection.Name)]
public sealed class BotOutageTests(BotOutageWebAppFixture app, PlaywrightFixture playwright) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await app.StopBotServiceAsync();
        await app.StartBotServiceAsync();
        await app.RefreshBotAvailabilityAsync();
    }

    private async Task<StartedVariantGame> HumanWhiteAgainstBotBlackAsync() =>
        await new VariantRoomBuilder(playwright.Browser, app.BaseUrl, GameKind.AliceChess)
            .WithHuman(Side.White, "Alice")
            .WithBot(Side.Black, BotDifficulty.Easy)
            .StartAsync();

    [Fact]
    public async Task The_lobby_shows_that_bots_are_unavailable_and_blocks_bot_games_until_they_are_back()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var room = await host.Home.CreateVariantRoomAsync(GameKind.AliceChess);
        await room.ClaimSeatAsync(Side.White, SeatRole.Player);
        await room.AddBotAsync(Side.Black, SeatRole.Player);
        await Expect(room.BotsUnavailableNotice).ToBeHiddenAsync();
        Assert.True(await room.IsStartGameEnabledAsync());

        await app.StopBotServiceAsync();
        await app.RefreshBotAvailabilityAsync();

        await Expect(room.BotsUnavailableNotice).ToBeVisibleAsync();
        await Expect(room.StartGameButton).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(room.SeatBotSelect(Side.Black, SeatRole.Player)).ToBeDisabledAsync();

        await room.ClearBotAsync(Side.Black, SeatRole.Player);
        await Expect(room.SeatJoinButton(Side.Black, SeatRole.Player)).ToBeVisibleAsync();
        await Expect(room.SeatAddBotButton(Side.Black, SeatRole.Player)).ToHaveCountAsync(0);

        await app.StartBotServiceAsync();
        await app.RefreshBotAvailabilityAsync();

        await Expect(room.BotsUnavailableNotice).ToBeHiddenAsync();
        await Expect(room.SeatAddBotButton(Side.Black, SeatRole.Player)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_game_pauses_with_a_countdown_while_the_bot_is_down_and_resumes_when_it_is_back()
    {
        var game = await HumanWhiteAgainstBotBlackAsync();
        await using var _ = game.Players[0];
        var white = game[Side.White];

        await app.StopBotServiceAsync();
        await white.MoveOnAsync(white.BoardA, "e2", "e4");

        await Expect(white.PausedBanner).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Expect(white.PausedCountdown).ToHaveTextAsync(new Regex(@"^[45]:\d\d$"));
        await Expect(white.ResignButton).ToBeVisibleAsync();
        var whiteClock = await white.ClockWhite.InnerTextAsync();
        var blackClock = await white.ClockBlack.InnerTextAsync();
        await white.Page.WaitForTimeoutAsync(2000);
        Assert.Equal(whiteClock, await white.ClockWhite.InnerTextAsync());
        Assert.Equal(blackClock, await white.ClockBlack.InnerTextAsync());

        await app.StartBotServiceAsync();

        await Expect(white.PausedBanner).ToBeHiddenAsync(new() { Timeout = 20000 });
        await Expect(white.MoveHistory.Locator("li")).ToHaveCountAsync(2);
        await white.ResignButton.ClickAsync();
        await Expect(white.GameOverBanner).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_game_whose_bot_stays_down_ends_as_bot_unavailable_and_says_so_in_the_history()
    {
        var game = await HumanWhiteAgainstBotBlackAsync();
        await using var _ = game.Players[0];
        var white = game[Side.White];

        await app.StopBotServiceAsync();
        await white.MoveOnAsync(white.BoardA, "e2", "e4");
        await Expect(white.PausedBanner).ToBeVisibleAsync(new() { Timeout = 15000 });

        app.Time.Advance(GamePause.Limit);

        await Expect(white.GameOverReason).ToHaveTextAsync("bot unavailable", new() { Timeout = 15000 });
        await Expect(white.GameOverWinner).ToHaveTextAsync("white");
        await Expect(white.GameOverExplanation).ToHaveTextAsync("The game ended because the bot stopped responding.");
        await Expect(white.PausedBanner).ToBeHiddenAsync();

        await white.Page.GotoAsync($"{app.BaseUrl}/history");
        var entry = white.Page.GetByTestId("history-list").Locator("li").First;
        await Expect(entry.GetByTestId("history-end-reason")).ToHaveTextAsync("ended because the bot was unavailable");

        await entry.Locator("a").ClickAsync();
        await white.Page.WaitForURLAsync(new Regex(@"/history/[0-9a-f-]{36}$"));
        await Expect(white.Page.GetByTestId("history-end-reason")).ToHaveTextAsync("ended because the bot was unavailable");
    }
}
