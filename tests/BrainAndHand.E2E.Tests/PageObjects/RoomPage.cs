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

    /// <summary>Shown instead of the seat grid once this room's one game has already finished —
    /// rooms aren't reusable for a rematch, so there's nothing left to configure here.</summary>
    public ILocator RoomRetiredMessage => Page.GetByTestId("room-retired");

    /// <summary>Host-only editable clock inputs. Non-host seats see <see cref="ClockSettingsDisplay"/> instead.</summary>
    public ILocator ClockMinutesInput => Page.GetByTestId("clock-initial-minutes");
    public ILocator ClockIncrementInput => Page.GetByTestId("clock-increment-seconds");
    public ILocator ClockSettingsDisplay => Page.GetByTestId("clock-settings-display");

    public ILocator Seat(Side side, SeatRole role) => Page.GetByTestId($"seat-{side}-{role}");
    public ILocator SeatJoinButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-join-btn");
    public ILocator SeatAddBotButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-add-bot-btn");

    /// <summary>Only present once the seat holds a bot — adding one always starts it at Medium
    /// (see <see cref="AddBotAsync"/>), and this lets the difficulty be changed afterward.</summary>
    public ILocator SeatBotSelect(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-bot-select");

    public ILocator SeatRemoveButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-remove-btn");
    public ILocator SeatLeaveButton(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-leave-btn");
    public ILocator SeatOccupantName(Side side, SeatRole role) => Seat(side, role).GetByTestId("seat-occupant-name");

    public async Task ClaimSeatAsync(Side side, SeatRole role)
    {
        await SeatJoinButton(side, role).ClickAsync();
        await SeatOccupantName(side, role).WaitForAsync();
    }

    /// <summary>Clicks "Add bot", which seats a Medium-difficulty bot — the only way to add one.</summary>
    public async Task AddBotAsync(Side side, SeatRole role)
    {
        await SeatAddBotButton(side, role).ClickAsync();
        await SeatBotSelect(side, role).WaitForAsync();
    }

    /// <summary>Adds a bot (always starts at Medium) and, if a different difficulty was asked for,
    /// adjusts it via the seat's difficulty dropdown.</summary>
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

    /// <summary>Host-only: changes both the initial minutes and the per-move increment together,
    /// matching how the two inputs are wired in the UI (each onchange sends both current values).</summary>
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

    /// <summary>Host-only: clicks "Start game" for a Card Chess room and follows the navigation to the board.</summary>
    public async Task<CardChessGamePage> StartCardChessGameAsync()
    {
        await StartGameButton.ClickAsync();
        return await WaitForCardChessGameStartedAsync();
    }

    /// <summary>Non-host seats: the room navigates them to the Card Chess board automatically once the host starts.</summary>
    public async Task<CardChessGamePage> WaitForCardChessGameStartedAsync(int timeoutMs = 15000)
    {
        await Page.WaitForURLAsync(new Regex($"/cardchess/{Regex.Escape(Code)}$"), new() { Timeout = timeoutMs });
        return new CardChessGamePage(Page);
    }
}
