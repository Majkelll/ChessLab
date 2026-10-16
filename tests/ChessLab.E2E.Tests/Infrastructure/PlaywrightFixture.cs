using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.Infrastructure;

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
