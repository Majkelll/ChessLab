using BrainAndHand.E2E.Tests.PageObjects;
using Microsoft.Playwright;

namespace BrainAndHand.E2E.Tests.Helpers;

/// <summary>
/// One "logged in" browser session: its own isolated context/cookies, signed in via the
/// <c>/TestAuth/Login</c> bypass (real Google OAuth can't be automated in tests).
/// </summary>
public sealed class TestPlayer : IAsyncDisposable
{
    public IBrowserContext Context { get; }
    public IPage Page { get; }
    public Guid UserId { get; }
    public string Name { get; }
    private readonly string baseUrl;

    private TestPlayer(IBrowserContext context, IPage page, Guid userId, string name, string baseUrl)
    {
        Context = context;
        Page = page;
        UserId = userId;
        Name = name;
        this.baseUrl = baseUrl;
    }

    public static async Task<TestPlayer> SignInAsync(IBrowser browser, string baseUrl, string name, Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{baseUrl}/TestAuth/Login?userId={id}&name={Uri.EscapeDataString(name)}");
        await page.WaitForURLAsync($"{baseUrl}/");
        // Wait for WASM to finish booting so the very first interaction isn't lost to a click that
        // lands before Blazor's event handlers are wired up.
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        return new TestPlayer(context, page, id, name, baseUrl);
    }

    public HomePage Home => new(Page);

    public async Task<RoomPage> GotoRoomAsync(string code)
    {
        await Page.GotoAsync($"{baseUrl}/room/{code}");
        return new RoomPage(Page);
    }

    public async Task<GamePage> GotoGameAsync(string code)
    {
        await Page.GotoAsync($"{baseUrl}/game/{code}");
        return new GamePage(Page);
    }

    public async ValueTask DisposeAsync() => await Context.CloseAsync();
}
