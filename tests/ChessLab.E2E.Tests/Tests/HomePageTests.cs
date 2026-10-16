namespace ChessLab.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class HomePageTests(WebAppFixture app, PlaywrightFixture playwright)
{
    [Fact]
    public async Task Creating_a_room_navigates_to_a_fresh_six_character_room_code()
    {
        await using var player = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");

        var room = await player.Home.CreateRoomAsync();

        Assert.Matches("^[A-Z0-9]{6}$", room.Code);
        await Expect(room.CodeDisplay).ToHaveTextAsync(room.Code);
    }

    [Fact]
    public async Task Joining_by_code_takes_a_second_player_into_the_same_room_the_first_created()
    {
        await using var host = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");
        var hostRoom = await host.Home.CreateRoomAsync();

        await using var guest = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Bob");
        var guestRoom = await guest.Home.JoinRoomByCodeAsync(hostRoom.Code);

        Assert.Equal(hostRoom.Code, guestRoom.Code);
    }

    [Fact]
    public async Task Joining_a_room_code_that_does_not_exist_shows_an_error_instead_of_a_board()
    {
        await using var player = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Alice");

        var room = await player.Home.JoinRoomByCodeAsync("ZZZZZZ");

        await Expect(room.ErrorMessage).ToContainTextAsync("not found");
    }
}
