namespace ChessLab.E2E.Tests.Tests;

/// <summary>
/// One game per new mode, played in a real browser against a real bot — the modes whose bots don't
/// need Stockfish, so these run anywhere. Each test drives the part of the UI that only that mode
/// has: two boards for Alice, pyramids for Martian, sealed bids for Bidding, a pool and a layout for
/// Draft.
/// </summary>
[Collection(AppCollection.Name)]
public sealed class VariantModesTests(WebAppFixture app, PlaywrightFixture playwright)
{
    [Fact]
    public async Task Alice_Chess_sends_a_moved_piece_to_the_other_board()
    {
        var game = await new VariantRoomBuilder(playwright.Browser, app.BaseUrl, GameKind.AliceChess)
            .WithHuman(Side.White, "Alice")
            .WithBot(Side.Black, BotDifficulty.Easy)
            .StartAsync();

        var white = game[Side.White];

        await Expect(white.BoardA).ToBeVisibleAsync();
        await Expect(white.BoardB).ToBeVisibleAsync();
        await Expect(white.SquareOn(white.BoardA, "e2")).ToBeVisibleAsync();

        await white.MoveOnAsync(white.BoardA, "e2", "e4");

        await white.WaitForMoveCountAsync(2);
        await Expect(white.MoveHistory).ToContainTextAsync("e4");
    }

    [Fact]
    public async Task Absorption_Chess_plays_a_move_and_answers_it()
    {
        var game = await new VariantRoomBuilder(playwright.Browser, app.BaseUrl, GameKind.AbsorptionChess)
            .WithHuman(Side.White, "Alice")
            .WithBot(Side.Black, BotDifficulty.Easy)
            .StartAsync();

        var white = game[Side.White];

        await Expect(white.AbsorbedPowers).ToBeVisibleAsync();

        await white.MoveAsync("e2", "e4");

        await white.WaitForMoveCountAsync(2);
    }

    [Fact]
    public async Task Martian_Chess_moves_a_pyramid_and_keeps_score()
    {
        var game = await new VariantRoomBuilder(playwright.Browser, app.BaseUrl, GameKind.MartianChess)
            .WithHuman(Side.White, "Alice")
            .WithBot(Side.Black, BotDifficulty.Easy)
            .StartAsync();

        var white = game[Side.White];

        await Expect(white.MartianBoard).ToBeVisibleAsync();
        await Expect(white.MartianPyramid("a1")).ToBeVisibleAsync();
        await Expect(white.ScoreWhite).ToContainTextAsync("0");

        await white.MoveMartianAsync("a3", "a5");

        await white.WaitForMoveCountAsync(2);
        await Expect(white.MoveHistory).ToContainTextAsync("a3-a5");
        await Expect(white.MartianPyramid("a3")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Bidding_Chess_pays_the_winner_of_the_bid_out_of_its_own_chips()
    {
        var game = await new VariantRoomBuilder(playwright.Browser, app.BaseUrl, GameKind.BiddingChess)
            .WithHuman(Side.White, "Alice")
            .WithBot(Side.Black, BotDifficulty.Easy)
            .StartAsync();

        var white = game[Side.White];

        await Expect(white.ChipsWhite).ToContainTextAsync("100");
        await Expect(white.ChipsBlack).ToContainTextAsync("100");

        await white.SubmitBidAsync(40);

        await Expect(white.Page.GetByTestId("last-bids")).ToBeVisibleAsync(new() { Timeout = 20000 });

        // Whoever won moves; if it was us, the board is ours to use, and either way the first move
        // of the game lands in the history.
        if (await white.MoveHistoryCountAsync() == 0)
            await white.MoveAsync("e2", "e4");

        await white.WaitForMoveCountAsync(1);
        await Expect(white.ChipsWhite).Not.ToContainTextAsync("100");
    }

    [Fact]
    public async Task Draft_Chess_builds_an_army_lays_it_out_and_starts_the_game()
    {
        var game = await new VariantRoomBuilder(playwright.Browser, app.BaseUrl, GameKind.DraftChess)
            .WithHuman(Side.White, "Alice")
            .WithBot(Side.Black, BotDifficulty.Easy)
            .StartAsync();

        var white = game[Side.White];

        await Expect(white.DraftPool).ToBeVisibleAsync();
        await white.DraftPick("Rook").ClickAsync();
        await Expect(white.DraftPassButton).ToBeEnabledAsync(new() { Timeout = 20000 });
        await white.DraftPassButton.ClickAsync();

        await Expect(white.PlacementTray).ToBeVisibleAsync(new() { Timeout = 20000 });

        await white.PlaceButton("King").ClickAsync();
        await white.Square("e1").ClickAsync();
        await white.PlaceButton("Rook").ClickAsync();
        await white.Square("a1").ClickAsync();

        await Expect(white.PlacementDone).ToBeVisibleAsync(new() { Timeout = 20000 });
        await white.WaitForTurnTextAsync("choosing a move", timeoutMs: 30000);
    }

    [Fact]
    public async Task Progressive_Chess_gives_black_two_moves_after_whites_one()
    {
        var game = await new VariantRoomBuilder(playwright.Browser, app.BaseUrl, GameKind.ProgressiveChess)
            .WithHuman(Side.White, "Alice")
            .WithHuman(Side.Black, "Bob")
            .StartAsync();

        var white = game[Side.White];
        var black = game[Side.Black];

        await Expect(white.SeriesCounter).ToContainTextAsync("series 1");

        await white.MoveAsync("e2", "e4");
        await black.WaitForMoveCountAsync(1);

        await black.WaitForTurnTextAsync("2 moves left");
        await black.MoveAsync("e7", "e5");
        await black.WaitForMoveCountAsync(2);

        await black.WaitForTurnTextAsync("last move of the series");
        await black.MoveAsync("b8", "c6");

        await white.WaitForMoveCountAsync(3);
        await Expect(white.SeriesCounter).ToContainTextAsync("series 3");
    }
}
