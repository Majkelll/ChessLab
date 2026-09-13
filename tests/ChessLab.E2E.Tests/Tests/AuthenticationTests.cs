namespace ChessLab.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class AuthenticationTests(WebAppFixture app, PlaywrightFixture playwright)
{
    [Fact]
    public async Task Anonymous_visitor_sees_the_landing_pitch_and_a_google_login_link()
    {
        await using var context = await playwright.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync(app.BaseUrl);

        var home = new HomePage(page);
        await home.AnonymousHeading.WaitForAsync();
        await Expect(page.GetByTestId("google-login-link")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Signing_in_shows_the_users_name_and_a_log_out_button()
    {
        await using var player = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Ada");

        await Expect(player.Home.Heading).ToContainTextAsync("Welcome, Ada!");
        await Expect(player.Page.GetByTestId("logged-in-name")).ToHaveTextAsync("Ada");
        await Expect(player.Page.GetByTestId("logout-btn")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Logging_out_returns_to_the_anonymous_landing_page()
    {
        await using var player = await TestPlayer.SignInAsync(playwright.Browser, app.BaseUrl, "Grace");

        await player.Page.GetByTestId("logout-btn").ClickAsync();

        await player.Home.AnonymousHeading.WaitForAsync();
    }

    [Theory]
    [InlineData("/room/AAAAAA")]
    [InlineData("/game/AAAAAA")]
    public async Task Anonymous_visitor_is_challenged_for_google_login_when_opening_a_protected_page(string path)
    {
        await using var context = await playwright.Browser.NewContextAsync();
        var page = await context.NewPageAsync();

        // [Authorize] + the default challenge scheme means the server redirects straight to
        // Google rather than rendering our page at all — assert we end up on Google's login,
        // without trying to complete a real Google sign-in (that can't be automated here).
        await page.GotoAsync($"{app.BaseUrl}{path}");

        Assert.StartsWith("https://accounts.google.com/", page.Url, StringComparison.Ordinal);
    }
}
