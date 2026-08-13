namespace BrainAndHand.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class GameEndingTests(WebAppFixture app, PlaywrightFixture playwright)
{
    private Task<StartedGame> StartFourHumanGameAsync() =>
        new GameRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, SeatRole.Brain, "Alice")
            .WithHuman(Side.White, SeatRole.Hand, "Bob")
            .WithHuman(Side.Black, SeatRole.Brain, "Carol")
            .WithHuman(Side.Black, SeatRole.Hand, "Dave")
            .StartAsync();

    [Fact]
    public async Task Checkmate_ends_the_game_for_every_seat_and_back_to_room_returns_to_the_lobby()
    {
        var game = await StartFourHumanGameAsync();

        await ChessScripts.PlayFoolsMateAsync(game);

        foreach (var seatGame in game.Games.Values)
        {
            var (reason, winner) = await seatGame.WaitForGameOverAsync();
            Assert.Equal("checkmate", reason);
            Assert.Equal("black", winner);
        }

        var roomPage = await game[Side.White, SeatRole.Brain].BackToRoomAsync();
        Assert.Equal(game.Code, roomPage.Code);
    }

    [Fact]
    public async Task Resigning_ends_the_game_in_favor_of_the_opponent_team()
    {
        var game = await StartFourHumanGameAsync();

        await game[Side.White, SeatRole.Brain].ResignAsync();

        foreach (var seatGame in game.Games.Values)
        {
            var (reason, winner) = await seatGame.WaitForGameOverAsync();
            Assert.Equal("resignation", reason);
            Assert.Equal("black", winner);
        }
    }

    [Fact]
    public async Task Once_the_game_is_over_the_resign_button_and_turn_status_are_replaced_by_the_banner()
    {
        var game = await StartFourHumanGameAsync();
        var whiteBrain = game[Side.White, SeatRole.Brain];

        await whiteBrain.ResignAsync();
        await whiteBrain.WaitForGameOverAsync();

        await Expect(whiteBrain.ResignButton).ToBeHiddenAsync();
        await Expect(whiteBrain.TurnStatus).ToBeHiddenAsync();
        await Expect(whiteBrain.BackToRoomLink).ToBeVisibleAsync();
    }
}
