namespace ChessLab.E2E.Tests.Tests;

/// <summary>
/// A whole game of each of the three original modes, played click by click in a real browser until
/// fifty moves have gone by or it ends on its own. These answer a different question from the rest
/// of the suite: not "does this feature work" but "can this mode be played, move after move,
/// without the board, the hub or the page getting stuck".
/// </summary>
[Trait("Category", "LongGame")]
[Collection(LongGameClassicsCollection.Name)]
public sealed class ClassicModeLongGameTests(WebAppFixture app, PlaywrightFixture playwright)
{
    private LongGamePlayer Player => new(app, playwright.Browser);

    [Fact]
    public Task Hand_and_Brain_plays_fifty_moves() => Player.PlayHandAndBrainAsync();

    [Fact]
    public Task Card_Chess_plays_fifty_moves() =>
        Player.PlayTwoSeatGameAsync(GameKind.CardChess, "/cardchess/");

    [Fact]
    public Task Arcane_Chess_plays_fifty_moves() =>
        Player.PlayTwoSeatGameAsync(GameKind.ArcaneChess, "/arcanechess/");
}

/// <summary>The new modes that are still played on one chessboard.</summary>
[Trait("Category", "LongGame")]
[Collection(LongGameChessLikeCollection.Name)]
public sealed class ChessLikeModeLongGameTests(WebAppFixture app, PlaywrightFixture playwright)
{
    private LongGamePlayer Player => new(app, playwright.Browser);

    [Fact]
    public Task Bidding_Chess_plays_fifty_moves() =>
        Player.PlayTwoSeatGameAsync(GameKind.BiddingChess, "/bidding/", LongGamePlayer.BidForBothSidesAsync);

    [Fact]
    public Task Progressive_Chess_plays_fifty_moves() =>
        Player.PlayTwoSeatGameAsync(GameKind.ProgressiveChess, "/progressive/");

    [Fact]
    public Task Absorption_Chess_plays_fifty_moves() =>
        Player.PlayTwoSeatGameAsync(GameKind.AbsorptionChess, "/absorption/");
}

/// <summary>The modes that don't look like a game of chess: two boards, pyramids, or an army that
/// has to be built before anything can move.</summary>
[Trait("Category", "LongGame")]
[Collection(LongGameOddOnesCollection.Name)]
public sealed class UnusualModeLongGameTests(WebAppFixture app, PlaywrightFixture playwright)
{
    private LongGamePlayer Player => new(app, playwright.Browser);

    [Fact]
    public Task Alice_Chess_plays_fifty_moves() =>
        Player.PlayTwoSeatGameAsync(GameKind.AliceChess, "/alice/");

    [Fact]
    public Task Martian_Chess_plays_fifty_moves() =>
        Player.PlayTwoSeatGameAsync(GameKind.MartianChess, "/martian/");

    [Fact]
    public Task Draft_Chess_plays_fifty_moves() =>
        Player.PlayTwoSeatGameAsync(GameKind.DraftChess, "/draft/",
            beforeFirstPly: LongGamePlayer.DraftAndLayOutBothArmiesAsync);
}
