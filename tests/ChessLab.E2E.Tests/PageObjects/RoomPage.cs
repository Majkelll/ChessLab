using System.Text.RegularExpressions;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.PageObjects;

public sealed class RoomPage
{
    public IPage Page { get; }

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

    public ILocator RoomRetiredMessage => Page.GetByTestId("room-retired");

    public ILocator ClockMinutesInput => Page.GetByTestId("clock-initial-minutes");
    public ILocator ClockIncrementInput => Page.GetByTestId("clock-increment-seconds");
    public ILocator ClockSettingsDisplay => Page.GetByTestId("clock-settings-display");

    public ILocator Seat(Side side, SeatRole role) => Page.GetByTestId($"seat-{side}-{role}");
    public ILocator SeatJoinButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-join-btn");
    public ILocator SeatAddBotButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-add-bot-btn");

    public ILocator SeatBotSelect(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-bot-select");

    public ILocator SeatRemoveButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-remove-btn");
    public ILocator SeatLeaveButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-leave-btn");
    public ILocator SeatOccupantName(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-occupant-name");

    public async Task ClaimSeatAsync(Side side, SeatRole role)
    {
        await SeatJoinButton(side, role).ClickAsync();
        await SeatOccupantName(side, role).WaitForAsync();
    }

    public async Task AddBotAsync(Side side, SeatRole role)
    {
        await SeatAddBotButton(side, role).ClickAsync();
        await SeatBotSelect(side, role).WaitForAsync();
    }

    public async Task SetBotAsync(Side side, SeatRole role, BotDifficulty difficulty)
    {
        await AddBotAsync(side, role);

        if (difficulty != BotDifficulty.Medium)
        {
            await SeatBotSelect(side, role).SelectOptionAsync(difficulty.ToString());
            await Expect(SeatBotSelect(side, role)).ToHaveValueAsync(difficulty.ToString());
        }
    }

    public async Task ClearBotAsync(Side side, SeatRole role)
    {
        await SeatRemoveButton(side, role).ClickAsync();
        await SeatJoinButton(side, role).WaitForAsync();
    }

    public async Task SetClockSettingsAsync(int minutes, int incrementSeconds)
    {
        await ClockMinutesInput.FillAsync(minutes.ToString());
        await ClockMinutesInput.DispatchEventAsync("change");
        await Expect(ClockMinutesInput).ToHaveValueAsync(minutes.ToString());

        await ClockIncrementInput.FillAsync(incrementSeconds.ToString());
        await ClockIncrementInput.DispatchEventAsync("change");
        await Expect(ClockIncrementInput).ToHaveValueAsync(incrementSeconds.ToString());
    }

    public async Task LeaveSeatAsync(Side side, SeatRole role)
    {
        await SeatLeaveButton(side, role).ClickAsync();
        await SeatJoinButton(side, role).WaitForAsync();
    }

    public async Task<bool> IsStartGameEnabledAsync() =>
        await StartGameButton.GetAttributeAsync("aria-disabled") == "false";

    public async Task<GamePage> StartGameAsync()
    {
        await StartGameButton.ClickAsync();
        return await WaitForGameStartedAsync();
    }

    public async Task<GamePage> WaitForGameStartedAsync(int timeoutMs = 15000)
    {
        await Page.WaitForURLAsync(new Regex($"/game/{Regex.Escape(Code)}$"), new() { Timeout = timeoutMs });
        return new GamePage(Page);
    }

    public async Task<CardChessGamePage> StartCardChessGameAsync()
    {
        await StartGameButton.ClickAsync();
        return await WaitForCardChessGameStartedAsync();
    }

    public async Task<CardChessGamePage> WaitForCardChessGameStartedAsync(int timeoutMs = 15000)
    {
        await Page.WaitForURLAsync(new Regex($"/cardchess/{Regex.Escape(Code)}$"), new() { Timeout = timeoutMs });
        return new CardChessGamePage(Page);
    }

    public async Task<ArcaneChessGamePage> StartArcaneChessGameAsync()
    {
        await StartGameButton.ClickAsync();
        return await WaitForArcaneChessGameStartedAsync();
    }

    public async Task<ArcaneChessGamePage> WaitForArcaneChessGameStartedAsync(int timeoutMs = 15000)
    {
        await Page.WaitForURLAsync(new Regex($"/arcanechess/{Regex.Escape(Code)}$"), new() { Timeout = timeoutMs });
        return new ArcaneChessGamePage(Page);
    }

    public async Task<VariantGamePage> StartVariantGameAsync(GameKind kind)
    {
        await StartGameButton.ClickAsync();
        return await WaitForVariantGameStartedAsync(kind);
    }

    public async Task<VariantGamePage> WaitForVariantGameStartedAsync(GameKind kind, int timeoutMs = 15000)
    {
        var route = Helpers.VariantRoutes.Of(kind);
        await Page.WaitForURLAsync(new Regex($"/{route}/{Regex.Escape(Code)}$"), new() { Timeout = timeoutMs });
        return new VariantGamePage(Page);
    }
}
