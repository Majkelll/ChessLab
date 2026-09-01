using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.PageObjects;

/// <summary>The "/" landing page: create a room, or join one by code.</summary>
public sealed class HomePage(IPage page)
{
    public IPage Page { get; } = page;

    public ILocator Heading => Page.GetByTestId("home-heading");
    public ILocator AnonymousHeading => Page.GetByTestId("home-heading-anon");
    public ILocator CreateRoomButton => Page.GetByTestId("create-room-btn");
    public ILocator CreateCardChessRoomButton => Page.GetByTestId("create-cardchess-room-btn");
    public ILocator JoinCodeInput => Page.GetByTestId("join-code-input");
    public ILocator JoinRoomButton => Page.GetByTestId("join-room-btn");
    public ILocator ErrorMessage => Page.GetByTestId("home-error");

    public async Task<RoomPage> CreateRoomAsync()
    {
        await CreateRoomButton.ClickAsync();
        await Page.WaitForURLAsync(new Regex(@"/room/[A-Z0-9]{6}$"));
        return new RoomPage(Page);
    }

    public async Task<RoomPage> CreateCardChessRoomAsync()
    {
        await CreateCardChessRoomButton.ClickAsync();
        await Page.WaitForURLAsync(new Regex(@"/room/[A-Z0-9]{6}$"));
        return new RoomPage(Page);
    }

    public async Task<RoomPage> JoinRoomByCodeAsync(string code)
    {
        await JoinCodeInput.FillAsync(code);
        await JoinRoomButton.ClickAsync();
        await Page.WaitForURLAsync(new Regex($"/room/{Regex.Escape(code)}$"));
        return new RoomPage(Page);
    }
}
