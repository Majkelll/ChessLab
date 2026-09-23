using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.PageObjects;

public sealed class VariantGamePage(IPage page)
{
    public IPage Page { get; } = page;

    public ILocator ClockWhite => Page.GetByTestId("clock-white");
    public ILocator ClockBlack => Page.GetByTestId("clock-black");
    public ILocator TurnStatus => Page.GetByTestId("turn-status");
    public ILocator MoveHistory => Page.GetByTestId("move-history");
    public ILocator GameOverBanner => Page.GetByTestId("game-over-banner");
    public ILocator GameOverReason => Page.GetByTestId("game-over-reason");
    public ILocator GameOverWinner => Page.GetByTestId("game-over-winner");
    public ILocator ResignButton => Page.GetByTestId("resign-btn");
    public ILocator BackToRoomLink => Page.GetByTestId("back-to-room-link");
    public ILocator ErrorMessage => Page.GetByTestId("game-error");

    public ILocator ChessBoardRoot => Page.GetByTestId("chess-board");
    public ILocator BoardA => Page.GetByTestId("alice-board-a");
    public ILocator BoardB => Page.GetByTestId("alice-board-b");
    public ILocator SeriesCounter => Page.GetByTestId("series-counter");
    public ILocator AbsorbedPowers => Page.GetByTestId("absorbed-powers");
    public ILocator ScoreWhite => Page.GetByTestId("score-white");
    public ILocator ScoreBlack => Page.GetByTestId("score-black");
    public ILocator ChipsWhite => Page.GetByTestId("chips-white");
    public ILocator ChipsBlack => Page.GetByTestId("chips-black");
    public ILocator BidInput => Page.GetByTestId("bid-input");
    public ILocator SubmitBidButton => Page.GetByTestId("submit-bid-btn");
    public ILocator BidSubmitted => Page.GetByTestId("bid-submitted");
    public ILocator DraftPool => Page.GetByTestId("draft-pool");
    public ILocator DraftPassButton => Page.GetByTestId("draft-pass-btn");
    public ILocator PlacementTray => Page.GetByTestId("placement-tray");
    public ILocator PlacementDone => Page.GetByTestId("placement-done");
    public ILocator MartianBoard => Page.GetByTestId("martian-board");

    public ILocator Square(string square) =>
        ChessBoardRoot.Locator($"rect.square[data-square='{square}']");

    public ILocator SquareOn(ILocator board, string square) =>
        board.Locator($"rect.square[data-square='{square}']");

    public ILocator MartianSquare(string square) => Page.GetByTestId($"martian-square-{square}");

    public ILocator MartianPyramid(string square) => Page.GetByTestId($"martian-pyramid-{square}");

    public ILocator DraftPick(string kind) => Page.GetByTestId($"draft-pick-{kind}");

    public ILocator PlaceButton(string kind) => Page.GetByTestId($"place-{kind}");

    public Task<int> MoveHistoryCountAsync() => MoveHistory.Locator("li").CountAsync();

    public Task MoveAsync(string from, string to) => MoveOnAsync(ChessBoardRoot, from, to);

    public Task MoveOnAsync(ILocator board, string from, string to) =>
        ClickThroughAsync(() => SquareOn(board, from), () => SquareOn(board, to));

    public Task MoveMartianAsync(string from, string to) =>
        ClickThroughAsync(() => MartianSquare(from), () => MartianSquare(to));

    private async Task ClickThroughAsync(Func<ILocator> from, Func<ILocator> to)
    {
        var before = await MoveHistoryCountAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await from().ClickAsync();
            await Page.WaitForTimeoutAsync(250);
            await to().ClickAsync();
            await Page.WaitForTimeoutAsync(400);

            if (await MoveHistoryCountAsync() > before)
                return;
        }
    }

    public async Task PassUntilLayingOutAsync(int timeoutMs = 30000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (await PlacementTray.CountAsync() > 0 && await PlacementTray.IsVisibleAsync())
                return;

            try
            {
                if (await DraftPassButton.IsVisibleAsync() && await DraftPassButton.IsEnabledAsync())
                    await DraftPassButton.ClickAsync(new() { Timeout = 2000 });
            }
            catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
            {
            }

            await Page.WaitForTimeoutAsync(200);
        }
    }

    public async Task SubmitBidAsync(int amount)
    {
        await BidInput.FillAsync(amount.ToString());
        await SubmitBidButton.ClickAsync();
    }

    public Task ResignAsync() => ResignButton.ClickAsync();

    public Task WaitForTurnTextAsync(string containingText, int timeoutMs = 10000) =>
        TurnStatus.Filter(new LocatorFilterOptions { HasText = containingText })
            .WaitForAsync(new() { Timeout = timeoutMs });

    public Task WaitForMoveCountAsync(int count, int timeoutMs = 20000) =>
        Expect(MoveHistory.Locator("li")).ToHaveCountAsync(count, new() { Timeout = timeoutMs });

    public async Task<(string Reason, string? Winner)> WaitForGameOverAsync(int timeoutMs = 20000)
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

    private static Microsoft.Playwright.ILocatorAssertions Expect(ILocator locator) =>
        Microsoft.Playwright.Assertions.Expect(locator);
}
