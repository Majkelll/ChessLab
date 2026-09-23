using System.Text.RegularExpressions;
using ChessLab.Core.Chess;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.PageObjects;

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

    /// <summary>The read-only "opponent: Knight" badge shown next to the opposing team's clock
    /// while it's their turn to move — only present for a seated player, and only once their
    /// opponents' Hand has an announced piece to act on.</summary>
    public ILocator OpponentPick => Page.GetByTestId("opponent-pick");

    public ILocator PieceCard(PieceKind kind) => Page.GetByTestId($"piece-card-{kind}");

    /// <summary>The board itself is rendered by cm-chessboard (https://github.com/shaack/cm-chessboard),
    /// not our own markup, so squares/promotion below are located via its native DOM (a "square"-classed
    /// rect carrying `data-square`, e.g. "e4") rather than a data-testid.</summary>
    public ILocator ChessBoardRoot => Page.GetByTestId("chess-board");

    /// <summary>cm-chessboard's pieces layer has `pointer-events: none`, so clicks/drags always land
    /// on the square rect beneath a piece, never the piece itself — this is the right (and only)
    /// element to target for both click-to-move and drag-and-drop.</summary>
    public ILocator Square(string square) => ChessBoardRoot.Locator($"rect.square[data-square='{square}']");

    public ILocator PromotionPicker => Page.Locator(".promotion-dialog-group");

    /// <summary>Matched by suffix ("...q", "...r", ...) since cm-chessboard's promotion buttons carry
    /// a color-prefixed piece code (e.g. "wq"/"bq") and only one side's dialog is ever shown at once.</summary>
    public ILocator PromotionChoice(PieceKind kind) => Page.Locator($".promotion-dialog-button-group[data-piece$='{PromotionLetter(kind)}']");

    private static string PromotionLetter(PieceKind kind) => kind switch
    {
        PieceKind.Queen => "q",
        PieceKind.Rook => "r",
        PieceKind.Bishop => "b",
        PieceKind.Knight => "n",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a legal promotion piece."),
    };

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
    /// <summary>Click the piece, then its destination — and try again if the board wasn't listening
    /// yet. The first click only arms the board's own move input, which a slow render can swallow,
    /// and a move that did land is never sent twice because the history is checked in between.</summary>
    public async Task MoveAsync(string from, string to)
    {
        var before = await MoveHistoryCountAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await Square(from).ClickAsync();
            await Page.WaitForTimeoutAsync(250);
            await Square(to).ClickAsync();
            await Page.WaitForTimeoutAsync(400);

            if (await MoveHistoryCountAsync() > before || await PromotionPicker.IsVisibleAsync())
                return;
        }
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
