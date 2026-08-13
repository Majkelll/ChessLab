using System.Text.RegularExpressions;
using BrainAndHand.Core.Chess;
using BrainAndHand.Core.Rooms;
using Microsoft.Playwright;

namespace BrainAndHand.E2E.Tests.PageObjects;

/// <summary>The "/room/{code}" lobby: seats, bots, and starting the game.</summary>
public sealed class RoomPage
{
    public IPage Page { get; }

    /// <summary>Captured once at construction — reading it live off <see cref="Page"/>.Url would race
    /// with the navigation to /game/{code} that <see cref="StartGameAsync"/>/<see cref="WaitForGameStartedAsync"/>
    /// are themselves waiting on.</summary>
    public string Code { get; }

    public RoomPage(IPage page)
    {
        Page = page;
        Code = new Uri(page.Url).Segments[^1].TrimEnd('/');
    }

    public ILocator CodeDisplay => Page.GetByTestId("room-code");
    public ILocator CopyLinkButton => Page.GetByTestId("copy-link-btn");
    public ILocator ErrorMessage => Page.GetByTestId("lobby-error");
    public ILocator FillSeatsHint => Page.GetByTestId("fill-seats-hint");
    public ILocator StartGameButton => Page.GetByTestId("start-game-btn");
    public ILocator WaitingForHostMessage => Page.GetByTestId("waiting-for-host");

    public ILocator Seat(Side side, SeatRole role) => Page.GetByTestId($"seat-{side}-{role}");
    public ILocator SeatJoinButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-join-btn");
    public ILocator SeatBotSelect(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-bot-select");
    public ILocator SeatRemoveButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-remove-btn");
    public ILocator SeatLeaveButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-leave-btn");
    public ILocator SeatOccupantName(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-occupant-name");
    public ILocator SeatBotLabel(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-bot-label");

    public async Task ClaimSeatAsync(Side side, SeatRole role)
    {
        await SeatJoinButton(side, role).ClickAsync();
        await SeatOccupantName(side, role).WaitForAsync();
    }

    public async Task SetBotAsync(Side side, SeatRole role, BotDifficulty difficulty)
    {
        await SeatBotSelect(side, role).SelectOptionAsync(difficulty.ToString());
        await SeatBotLabel(side, role).WaitForAsync();
    }

    public async Task ClearBotAsync(Side side, SeatRole role)
    {
        await SeatRemoveButton(side, role).ClickAsync();
        await SeatJoinButton(side, role).WaitForAsync();
    }

    public async Task LeaveSeatAsync(Side side, SeatRole role)
    {
        await SeatLeaveButton(side, role).ClickAsync();
        await SeatJoinButton(side, role).WaitForAsync();
    }

    public async Task<bool> IsStartGameEnabledAsync() =>
        await StartGameButton.GetAttributeAsync("aria-disabled") == "false";

    /// <summary>Host-only: clicks "Start game" and follows the navigation to the board.</summary>
    public async Task<GamePage> StartGameAsync()
    {
        await StartGameButton.ClickAsync();
        return await WaitForGameStartedAsync();
    }

    /// <summary>Non-host seats: the room navigates them to the board automatically once the host starts.</summary>
    public async Task<GamePage> WaitForGameStartedAsync(int timeoutMs = 15000)
    {
        await Page.WaitForURLAsync(new Regex($"/game/{Regex.Escape(Code)}$"), new() { Timeout = timeoutMs });
        return new GamePage(Page);
    }
}
