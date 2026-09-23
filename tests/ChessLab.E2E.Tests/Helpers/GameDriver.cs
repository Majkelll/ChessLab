using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.Helpers;

public sealed class GameDriver(IPage page, Random random)
{
    private const int FirstAttemptTimeoutMs = 1200;

    private const int RetryTimeoutMs = 2500;

    private const int SyncTimeoutMs = 15000;

    private const int SettleMs = 250;

    public IPage Page { get; } = page;

    public int Attempts { get; private set; }

    public int Retries { get; private set; }

    public ILocator ChessBoard => Page.GetByTestId("chess-board");

    public ILocator MartianBoard => Page.GetByTestId("martian-board");

    public ILocator MoveHistory => Page.GetByTestId("move-history");

    public ILocator TurnIndicator => Page.Locator("[data-turn-indicator]");

    public ILocator GameOverBanner => Page.GetByTestId("game-over-banner");

    public ILocator ErrorMessage => Page.GetByTestId("game-error");

    public Task<int> MoveCountAsync() => MoveHistory.Locator("li").CountAsync();

    public Task<bool> IsGameOverAsync() => GameOverBanner.IsVisibleAsync();

    public async Task<string?> SideToMoveAsync() =>
        await TurnIndicator.CountAsync() > 0 ? (await TurnIndicator.First.InnerTextAsync()).Trim() : null;

    public async Task<string?> ErrorTextAsync() =>
        await ErrorMessage.CountAsync() > 0 ? await ErrorMessage.InnerTextAsync() : null;

    public async Task<IReadOnlyList<PlayableBoard>> BoardsWithMovesAsync()
    {
        var boards = new List<PlayableBoard>();

        foreach (var board in await AllBoardsAsync())
        {
            var moves = (await board.Locator.GetAttributeAsync("data-available-moves") ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (moves.Length > 0)
                boards.Add(board with { Moves = moves });
        }

        return boards;
    }

    public async Task<bool> WaitForReadyAsync(int timeoutMs = 20000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (await IsGameOverAsync() || await SideToMoveAsync() is not null)
                return true;

            await Page.WaitForTimeoutAsync(100);
        }

        return false;
    }

    public async Task<IReadOnlyList<PlayableBoard>> WaitForMovesAsync(int timeoutMs = 10000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (true)
        {
            var boards = await BoardsWithMovesAsync();
            if (boards.Count > 0 || DateTime.UtcNow >= deadline)
                return boards;

            await Page.WaitForTimeoutAsync(100);
        }
    }

    public async Task<bool> PlayRandomMoveAsync(int movesPlayedSoFar)
    {
        if (await WaitForMoveCountAsync(movesPlayedSoFar, SyncTimeoutMs))
            await Page.WaitForTimeoutAsync(SettleMs);

        var boards = await WaitForMovesAsync();
        if (boards.Count == 0)
            return false;

        var board = boards[random.Next(boards.Count)];
        var move = board.Moves[random.Next(board.Moves.Length)];
        return await PlayAsync(board, move);
    }

    private async Task<bool> PlayAsync(PlayableBoard board, string move)
    {
        var before = await MoveCountAsync();
        var from = move[..2];
        var to = move.Substring(2, 2);
        var promotion = move.Length > 4 ? move[4] : (char?)null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            Attempts++;
            if (attempt > 0)
                Retries++;

            await SquareOn(board, from).ClickAsync();
            await Page.WaitForTimeoutAsync(60);
            await SquareOn(board, to).ClickAsync();

            if (promotion is { } piece)
                await ChoosePromotionAsync(piece);

            var timeout = attempt == 0 ? FirstAttemptTimeoutMs : RetryTimeoutMs;
            if (await WaitForMoveCountAsync(before + 1, timeout))
                return true;

            await Page.WaitForTimeoutAsync(SettleMs);
        }

        return false;
    }

    private async Task ChoosePromotionAsync(char piece)
    {
        var choice = Page.Locator($".promotion-dialog-button-group[data-piece$='{piece}']");

        try
        {
            await choice.ClickAsync(new() { Timeout = 2000 });
        }
        catch (TimeoutException)
        {
        }
    }

    public async Task<bool> WaitForMoveCountAsync(int expected, int timeoutMs)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (true)
        {
            if (await MoveCountAsync() >= expected)
                return true;

            if (DateTime.UtcNow >= deadline)
                return false;

            await Page.WaitForTimeoutAsync(40);
        }
    }

    private async Task<IReadOnlyList<PlayableBoard>> AllBoardsAsync()
    {
        var chessBoards = await ChessBoard.CountAsync();
        if (chessBoards > 0)
        {
            return
            [
                .. Enumerable.Range(0, chessBoards)
                    .Select(index => new PlayableBoard(ChessBoard.Nth(index), IsMartian: false, [])),
            ];
        }

        return await MartianBoard.CountAsync() > 0
            ? [new PlayableBoard(MartianBoard, IsMartian: true, [])]
            : [];
    }

    private ILocator SquareOn(PlayableBoard board, string square) =>
        board.IsMartian
            ? Page.GetByTestId($"martian-square-{square}")
            : board.Locator.Locator($"rect.square[data-square='{square}']");
}

public sealed record PlayableBoard(ILocator Locator, bool IsMartian, string[] Moves);
