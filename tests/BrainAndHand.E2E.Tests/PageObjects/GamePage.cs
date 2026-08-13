using System.Text.RegularExpressions;
using BrainAndHand.Core.Chess;
using Microsoft.Playwright;

namespace BrainAndHand.E2E.Tests.PageObjects;

/// <summary>The "/game/{code}" board: cards, moves, clocks, resigning, and the game-over banner.</summary>
public sealed class GamePage(IPage page)
{
    public IPage Page { get; } = page;

    public ILocator ClockWhite => Page.GetByTestId("clock-white");
    public ILocator ClockBlack => Page.GetByTestId("clock-black");
    public ILocator TurnStatus => Page.GetByTestId("turn-status");
    public ILocator GameOverBanner => Page.GetByTestId("game-over-banner");
    public ILocator GameOverReason => Page.GetByTestId("game-over-reason");
    public ILocator GameOverWinner => Page.GetByTestId("game-over-winner");
    public ILocator BackToRoomLink => Page.GetByTestId("back-to-room-link");
    public ILocator ResignButton => Page.GetByTestId("resign-btn");
    public ILocator MoveHistory => Page.GetByTestId("move-history");
    public ILocator ErrorMessage => Page.GetByTestId("game-error");
    public ILocator PieceKindCards => Page.GetByTestId("piece-kind-cards");
    public ILocator PromotionPicker => Page.GetByTestId("promotion-picker");

    public ILocator PieceCard(PieceKind kind) => Page.GetByTestId($"piece-card-{kind}");
    public ILocator Square(string square) => Page.GetByTestId($"square-{square}");
    public ILocator PromotionChoice(PieceKind kind) => Page.GetByTestId($"promote-{kind}");

    public Task<int> MoveHistoryCountAsync() => MoveHistory.Locator("li").CountAsync();

    public Task<string> GetTurnStatusAsync() => TurnStatus.InnerTextAsync();

    public async Task<bool> IsPieceCardEnabledAsync(PieceKind kind) =>
        await PieceCard(kind).GetAttributeAsync("aria-disabled") == "false";

    /// <summary>True for whichever card is currently highlighted as the announced piece kind —
    /// visible to both Brain and Hand, and to the Hand specifically even though their cards
    /// aren't clickable, so they can see what they're allowed to move.</summary>
    public async Task<bool> IsPieceCardAnnouncedAsync(PieceKind kind) =>
        await PieceCard(kind).GetAttributeAsync("aria-current") == "true";

    public Task SelectPieceKindAsync(PieceKind kind) => PieceCard(kind).ClickAsync();

    /// <summary>Click-to-move: click the origin square, then the destination square. The short pause
    /// after the first click matters: Blazor WebAssembly's event dispatch hops through a JS interop
    /// microtask even for a fully synchronous handler, so two Playwright clicks fired back-to-back
    /// can both land before the first one's handler has actually run and recorded the selection.</summary>
    public async Task MoveAsync(string from, string to)
    {
        await Square(from).ClickAsync();
        await Page.WaitForTimeoutAsync(100);
        await Square(to).ClickAsync();
    }

    /// <summary>Drag-and-drop move, exercised separately from click-to-move so both input paths get coverage.</summary>
    public async Task DragMoveAsync(string from, string to) =>
        await Square(from).DragToAsync(Square(to));

    public Task PromoteToAsync(PieceKind kind) => PromotionChoice(kind).ClickAsync();

    public Task ResignAsync() => ResignButton.ClickAsync();

    /// <summary>Waits until this player's turn-status shows the given phase text (e.g. after another
    /// seat's action, before this page's board/cards can be trusted to reflect the new state).</summary>
    public Task WaitForTurnTextAsync(string containingText, int timeoutMs = 10000) =>
        TurnStatus.Filter(new LocatorFilterOptions { HasText = containingText }).WaitForAsync(new() { Timeout = timeoutMs });

    public Task<bool> IsGameOverAsync() => GameOverBanner.IsVisibleAsync();

    public async Task<(string Reason, string? Winner)> WaitForGameOverAsync(int timeoutMs = 15000)
    {
        await GameOverBanner.WaitForAsync(new() { Timeout = timeoutMs });
        var reason = await GameOverReason.InnerTextAsync();
        var winner = await GameOverWinner.CountAsync() > 0 ? await GameOverWinner.InnerTextAsync() : null;
        return (reason, winner);
    }

    public async Task<RoomPage> BackToRoomAsync()
    {
        await BackToRoomLink.ClickAsync();
        // Matches how every other page object waits for a Blazor client-side navigation.
        await Page.WaitForURLAsync(new Regex("/room/"), new() { Timeout = 15000 });
        return new RoomPage(Page);
    }
}
