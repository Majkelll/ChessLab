using Microsoft.Playwright;

namespace BrainAndHand.E2E.Tests.Infrastructure;

/// <summary>One headless Chromium instance shared by every test in a collection.</summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright playwright = null!;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    public async Task DisposeAsync()
    {
        await Browser.CloseAsync();
        playwright.Dispose();
    }
}
