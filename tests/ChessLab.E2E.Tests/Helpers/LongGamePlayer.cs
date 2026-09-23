using ChessLab.Core.Draft;
using ChessLab.E2E.Tests.Infrastructure;
using Microsoft.Playwright;

namespace ChessLab.E2E.Tests.Helpers;

/// <summary>
/// Plays a whole game of one mode through the browser, move after move, and fails if the room
/// stops responding before it has played its fifty. Everything mode-specific lives here — the
/// sealed bids, the draft and its layout, the Brain's announcement — so the tests themselves are
/// one line each.
/// </summary>
public sealed class LongGamePlayer(WebAppFixture app, IBrowser browser)
{
    public const int TargetPlies = 50;

    public async Task PlayHandAndBrainAsync()
    {
        var host = await TestPlayer.SignInAsync(browser, app.BaseUrl, "WhiteBrain");
        var room = await host.Home.CreateRoomAsync();
        await room.ClaimSeatAsync(Side.White, SeatRole.Brain);

        var others = new List<TestPlayer>();
        foreach (var (side, role, name) in ((Side, SeatRole, string)[])
        [
            (Side.White, SeatRole.Hand, "WhiteHand"),
            (Side.Black, SeatRole.Brain, "BlackBrain"),
            (Side.Black, SeatRole.Hand, "BlackHand"),
        ])
        {
            var player = await TestPlayer.SignInAsync(browser, app.BaseUrl, name);
            var joined = await player.GotoRoomAsync(room.Code);
            await joined.ClaimSeatAsync(side, role);
            others.Add(player);
        }

        await room.StartGameAsync();
        foreach (var player in others)
            await player.Page.WaitForURLAsync(new System.Text.RegularExpressions.Regex($"/game/{room.Code}$"));

        var rng = new Random(7);
        var brains = new Dictionary<Side, IPage> { [Side.White] = host.Page, [Side.Black] = others[1].Page };
        var hands = new Dictionary<Side, IPage> { [Side.White] = others[0].Page, [Side.Black] = others[2].Page };
        var drivers = hands.ToDictionary(entry => entry.Key, entry => new GameDriver(entry.Value, rng));

        var played = 0;
        var freshest = drivers[Side.White];

        while (played < TargetPlies)
        {
            await freshest.WaitForReadyAsync();
            if (await freshest.IsGameOverAsync() || await freshest.SideToMoveAsync() is not { } side)
                break;

            var mover = side == "white" ? Side.White : Side.Black;
            if (!await AnnounceAPieceKindAsync(brains[mover], drivers[mover], rng))
                break;

            if (!await drivers[mover].PlayRandomMoveAsync(played))
                break;

            freshest = drivers[mover];
            played++;
        }

        await AssertPlayedOutAsync(freshest, played);
    }

    public async Task PlayTwoSeatGameAsync(
        GameKind kind,
        string route,
        Func<IReadOnlyDictionary<Side, GameDriver>, Random, Task>? beforeEachPly = null,
        Func<IReadOnlyDictionary<Side, GameDriver>, Random, Task>? beforeFirstPly = null)
    {
        var rng = new Random(11);
        var pages = await StartTwoSeatGameAsync(kind, route);
        var drivers = pages.ToDictionary(entry => entry.Key, entry => new GameDriver(entry.Value, rng));

        if (beforeFirstPly is not null)
            await beforeFirstPly(drivers, rng);

        var played = 0;
        var freshest = drivers[Side.White];

        while (played < TargetPlies)
        {
            await freshest.WaitForReadyAsync();
            if (await freshest.IsGameOverAsync())
                break;

            if (beforeEachPly is not null)
                await beforeEachPly(drivers, rng);

            if (await freshest.IsGameOverAsync() || await freshest.SideToMoveAsync() is not { } side)
                break;

            var mover = drivers[side == "white" ? Side.White : Side.Black];
            if (!await mover.PlayRandomMoveAsync(played))
                break;

            freshest = mover;
            played++;
        }

        await AssertPlayedOutAsync(freshest, played);
    }

    private async Task<IReadOnlyDictionary<Side, IPage>> StartTwoSeatGameAsync(GameKind kind, string route)
    {
        var white = await TestPlayer.SignInAsync(browser, app.BaseUrl, "White");
        var whiteRoom = kind switch
        {
            GameKind.CardChess => await white.Home.CreateCardChessRoomAsync(),
            GameKind.ArcaneChess => await white.Home.CreateArcaneChessRoomAsync(),
            _ => await white.Home.CreateVariantRoomAsync(kind),
        };
        await whiteRoom.ClaimSeatAsync(Side.White, SeatRole.Player);

        var black = await TestPlayer.SignInAsync(browser, app.BaseUrl, "Black");
        var blackRoom = await black.GotoRoomAsync(whiteRoom.Code);
        await blackRoom.ClaimSeatAsync(Side.Black, SeatRole.Player);

        await whiteRoom.StartGameButton.ClickAsync();

        var url = new System.Text.RegularExpressions.Regex($"{route}{whiteRoom.Code}$");
        await white.Page.WaitForURLAsync(url, new() { Timeout = 20000 });
        await black.Page.WaitForURLAsync(url, new() { Timeout = 20000 });

        return new Dictionary<Side, IPage> { [Side.White] = white.Page, [Side.Black] = black.Page };
    }

    /// <summary>Nobody moves in Bidding Chess until both sides have put chips on it, and a page
    /// only shows its bid box once its own turn to bid has arrived — so this keeps asking until
    /// neither side is still holding one.</summary>
    public static async Task BidForBothSidesAsync(IReadOnlyDictionary<Side, GameDriver> drivers, Random rng)
    {
        for (var round = 0; round < 20; round++)
        {
            var outstanding = false;

            foreach (var driver in drivers.Values)
            {
                if (!await IsShowingAsync(driver.Page.GetByTestId("submit-bid-btn")))
                    continue;

                outstanding = true;
                await driver.Page.GetByTestId("bid-input").FillAsync(rng.Next(0, 12).ToString());
                await ClickIfPossibleAsync(driver.Page.GetByTestId("submit-bid-btn"));
            }

            if (!outstanding)
                return;

            await drivers[Side.White].Page.WaitForTimeoutAsync(150);
        }
    }

    /// <summary>Runs the draft the way two players would: whoever's pick it is takes something,
    /// and once both armies are big enough they pass, which is what moves the room on to laying the
    /// pieces out.</summary>
    public static async Task DraftAndLayOutBothArmiesAsync(
        IReadOnlyDictionary<Side, GameDriver> drivers, Random rng)
    {
        var pool = new[] { "Rook", "Knight", "Bishop", "Pawn" };
        var deadline = DateTime.UtcNow.AddSeconds(90);
        var picks = 0;

        foreach (var driver in drivers.Values)
            await driver.Page.GetByTestId("draft-pool").WaitForAsync(new() { Timeout = 30000 });

        while (DateTime.UtcNow < deadline)
        {
            if (await BothAreLayingOutAsync(drivers))
                break;

            var acted = false;

            foreach (var driver in drivers.Values)
            {
                if (picks < 16)
                {
                    foreach (var kind in pool)
                    {
                        if (!await ClickIfPossibleAsync(driver.Page.GetByTestId($"draft-pick-{kind}")))
                            continue;

                        picks++;
                        acted = true;
                        break;
                    }
                }

                if (!acted)
                    acted = await ClickIfPossibleAsync(driver.Page.GetByTestId("draft-pass-btn"));

                if (acted)
                    break;
            }

            if (!acted)
                await drivers[Side.White].Page.WaitForTimeoutAsync(100);
        }

        if (!await BothAreLayingOutAsync(drivers))
        {
            var state = new List<string>();
            foreach (var (side, driver) in drivers)
            {
                var summary = await driver.Page.GetByTestId("draft-summary").InnerTextAsync();
                var stillDrafting = await IsShowingAsync(driver.Page.GetByTestId("draft-pool"));
                var canPass = await IsShowingAsync(driver.Page.GetByTestId("draft-pass-btn")) &&
                    await driver.Page.GetByTestId("draft-pass-btn").IsEnabledAsync();
                state.Add($"{side}: drafting={stillDrafting} passEnabled={canPass} picks={picks} " +
                    $"summary={summary.Replace('\n', '/')}");
            }

            throw new InvalidOperationException($"The draft never finished. {string.Join(" || ", state)}");
        }

        _ = rng;

        foreach (var (side, driver) in drivers)
            await LayOutOneArmyAsync(side, driver);
    }

    private static async Task<bool> BothAreLayingOutAsync(IReadOnlyDictionary<Side, GameDriver> drivers)
    {
        foreach (var driver in drivers.Values)
        {
            if (!await IsShowingAsync(driver.Page.GetByTestId("placement-tray")))
                return false;
        }

        return true;
    }

    /// <summary>Lays one army out square by square. The squares come from the test's own list
    /// rather than from reading the board back: every placement uses a fresh one, so nothing has to
    /// be verified between clicks.</summary>
    private static async Task LayOutOneArmyAsync(Side side, GameDriver driver)
    {
        await driver.Page.GetByTestId("placement-tray").WaitForAsync(new() { Timeout = 30000 });

        var backRank = side == Side.White ? '1' : '8';
        var pawnRank = side == Side.White ? '2' : '7';
        var officerFiles = new Queue<char>("edcfbgah");
        var pawnFiles = new Queue<char>("abcdefgh");
        var steps = new List<string>();

        for (var placed = 0; placed < GameState.MaxOfficers + GameState.MaxPawns + 2; placed++)
        {
            var tray = driver.Page.GetByTestId("placement-tray").Locator("[data-testid^='place-']");
            if (await tray.CountAsync() == 0)
                break;

            var kind = (await tray.First.GetAttributeAsync("data-testid"))!["place-".Length..];
            if (!await ClickIfPossibleAsync(tray.First))
            {
                steps.Add($"could not pick up {kind}");
                break;
            }

            var isPawn = kind == "Pawn";
            var files = isPawn ? pawnFiles : officerFiles;
            if (files.Count == 0)
            {
                steps.Add($"no square left for {kind}");
                break;
            }

            var square = $"{files.Dequeue()}{(isPawn ? pawnRank : backRank)}";
            await driver.Page.GetByTestId("chess-board")
                .Locator($"rect.square[data-square='{square}']").ClickAsync();
            await driver.Page.WaitForTimeoutAsync(120);
            steps.Add($"{kind}->{square}");
        }

        // Either this side is waiting for the other ("ready"), or it laid out last and the room has
        // already moved on to the game, taking the tray with it.
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (await IsShowingAsync(driver.Page.GetByTestId("placement-done")) ||
                !await IsShowingAsync(driver.Page.GetByTestId("placement-tray")))
            {
                return;
            }

            await driver.Page.WaitForTimeoutAsync(100);
        }

        var left = await driver.Page.Locator("[data-testid^='place-']")
            .EvaluateAllAsync<string[]>("els => els.map(e => e.innerText.trim())");

        throw new InvalidOperationException(
            $"{side} never finished laying out. Placed: {string.Join(", ", steps)}. Left: {string.Join(", ", left)}.");
    }

    /// <summary>The Brain has to name a piece kind before the Hand has anything to click, and a
    /// card the Brain can't announce is greyed out rather than missing.</summary>
    private static async Task<bool> AnnounceAPieceKindAsync(IPage brain, GameDriver hand, Random rng)
    {
        try
        {
            await brain.GetByTestId("piece-kind-cards").WaitForAsync(new() { Timeout = 15000 });
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return false;
        }

        var cards = brain.Locator("[data-testid^='piece-card-']");
        var order = Enumerable.Range(0, await cards.CountAsync()).OrderBy(_ => rng.Next()).ToArray();

        foreach (var offset in order)
        {
            var card = cards.Nth(offset);
            if (await card.GetAttributeAsync("aria-disabled") != "false")
                continue;

            await card.ClickAsync();

            if ((await hand.WaitForMovesAsync(3000)).Count > 0)
                return true;
        }

        return (await hand.WaitForMovesAsync(1000)).Count > 0;
    }

    private static async Task<bool> IsShowingAsync(ILocator locator)
    {
        try
        {
            return await locator.IsVisibleAsync();
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Clicks the control if it is there and usable, and shrugs if it vanished between
    /// looking and clicking — which a page that re-renders on every update does a lot.</summary>
    private static async Task<bool> ClickIfPossibleAsync(ILocator locator)
    {
        try
        {
            if (!await locator.IsVisibleAsync() || !await locator.IsEnabledAsync(new() { Timeout = 3000 }))
                return false;

            await locator.ClickAsync(new() { Timeout = 5000 });
            return true;
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
            return false;
        }
    }

    private static async Task AssertPlayedOutAsync(GameDriver driver, int played)
    {
        Assert.Null(await driver.ErrorTextAsync());

        if (played >= TargetPlies)
            return;

        if (!await driver.IsGameOverAsync())
        {
            var moves = await driver.ChessBoard.CountAsync() > 0
                ? await driver.ChessBoard.First.GetAttributeAsync("data-available-moves")
                : await driver.MartianBoard.GetAttributeAsync("data-available-moves");
            var status = await driver.Page.GetByTestId("turn-status").InnerTextAsync();

            Assert.Fail($"Only {played} of {TargetPlies} moves were played and the game is still " +
                $"running. Status: {status.Replace('\n', ' ')}. Moves on offer: {moves}.");
        }

        var reason = await driver.Page.GetByTestId("game-over-reason").InnerTextAsync();
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }
}
