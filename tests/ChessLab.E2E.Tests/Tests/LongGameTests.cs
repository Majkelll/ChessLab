namespace ChessLab.E2E.Tests.Tests;

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
