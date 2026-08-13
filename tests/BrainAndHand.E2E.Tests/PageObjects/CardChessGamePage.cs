using System.Text.RegularExpressions;
using BrainAndHand.Core.CardChess;
using BrainAndHand.Core.Chess;
using Microsoft.Playwright;

namespace BrainAndHand.E2E.Tests.PageObjects;

/// <summary>The "/cardchess/{code}" board: draws, moves, HP, clocks, resigning, and the game-over banner.</summary>
public sealed class CardChessGamePage(IPage page)
{
    public IPage Page { get; } = page;

    public ILocator ClockWhite => Page.GetByTestId("clock-white");
    public ILocator ClockBlack => Page.GetByTestId("clock-black");
    public ILocator HpWhite => Page.GetByTestId("hp-white");
    public ILocator HpBlack => Page.GetByTestId("hp-black");
    public ILocator TurnStatus => Page.GetByTestId("turn-status");
    public ILocator EmergencyMoveBanner => Page.GetByTestId("emergency-move-banner");
    public ILocator NoPlayableCardBanner => Page.GetByTestId("no-playable-card-banner");
    public ILocator MyHand => Page.GetByTestId("my-hand");
    public ILocator OpponentHand => Page.GetByTestId("opponent-hand");
    public ILocator GameOverBanner => Page.GetByTestId("game-over-banner");
    public ILocator GameOverReason => Page.GetByTestId("game-over-reason");
    public ILocator GameOverWinner => Page.GetByTestId("game-over-winner");
    public ILocator BackToRoomLink => Page.GetByTestId("back-to-room-link");
    public ILocator ResignButton => Page.GetByTestId("resign-btn");
    public ILocator MoveHistory => Page.GetByTestId("move-history");
    public ILocator ErrorMessage => Page.GetByTestId("game-error");

    /// <summary>Same cm-chessboard-rendered board as the Hand &amp; Brain game page — see
    /// <see cref="GamePage"/>'s equivalent members for why squares are located this way.</summary>
    public ILocator ChessBoardRoot => Page.GetByTestId("chess-board");
    public ILocator Square(string square) => ChessBoardRoot.Locator($"rect.square[data-square='{square}']");
    public ILocator PromotionPicker => Page.Locator(".promotion-dialog-group");
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

    /// <summary>The rank (e.g. Six, Ten, Queen) of each card currently in this seat's hand, read
    /// from each card's <c>data-testid</c> rather than its displayed content — the hand renders
    /// piece glyphs, not rank text, so this is the only reliable way to know which cards they are.
    /// Uses <c>EvaluateAllAsync</c> rather than <c>Locator.AllAsync()</c>, which snapshots the DOM
    /// immediately with no auto-waiting and can race a hand that hasn't rendered yet. The deck is
    /// shuffled server-side, so tests read the hand rather than controlling it directly.</summary>
    public async Task<IReadOnlyList<CardRank>> GetHandCardRanksAsync()
    {
        var testIds = await MyHand.Locator("[data-testid^='hand-card-']")
            .EvaluateAllAsync<string[]>("els => els.map(e => e.getAttribute('data-testid'))");
        return testIds.Select(id => Enum.Parse<CardRank>(id["hand-card-".Length..])).ToArray();
    }

    /// <summary>Click-to-move — see <see cref="GamePage.MoveAsync"/> for why the short pause matters.</summary>
    public async Task MoveAsync(string from, string to)
    {
        await Square(from).ClickAsync();
        await Page.WaitForTimeoutAsync(100);
        await Square(to).ClickAsync();
    }

    public async Task DragMoveAsync(string from, string to) =>
        await Square(from).DragToAsync(Square(to));

    public Task PromoteToAsync(PieceKind kind) => PromotionChoice(kind).ClickAsync();

    public Task ResignAsync() => ResignButton.ClickAsync();

    /// <summary>Waits until this player's turn-status shows the given phase text.</summary>
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
        await Page.WaitForURLAsync(new Regex("/room/"), new() { Timeout = 15000 });
        return new RoomPage(Page);
    }
}
