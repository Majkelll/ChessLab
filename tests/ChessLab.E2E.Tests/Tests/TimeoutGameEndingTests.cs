namespace ChessLab.E2E.Tests.Tests;

[Collection(ShortClockAppCollection.Name)]
public sealed class TimeoutGameEndingTests(ShortClockWebAppFixture app, PlaywrightFixture playwright)
{
    [Fact]
    public async Task A_sides_clock_running_out_ends_the_game_in_favor_of_the_opponent_with_no_player_action()
    {
        var game = await new GameRoomBuilder(playwright.Browser, app.BaseUrl)
            .WithHuman(Side.White, SeatRole.Brain, "Alice")
            .WithHuman(Side.White, SeatRole.Hand, "Bob")
            .WithHuman(Side.Black, SeatRole.Brain, "Carol")
            .WithHuman(Side.Black, SeatRole.Hand, "Dave")
            .StartAsync();

        foreach (var seatGame in game.Games.Values)
        {
            var (reason, winner) = await seatGame.WaitForGameOverAsync(timeoutMs: 10000);
            Assert.Equal("timeout", reason);
            Assert.Equal("black", winner);
        }
    }
}
