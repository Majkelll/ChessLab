using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Suites;

public class NotationTests
{
    [Theory]
    [InlineData(GameKind.AliceChess, "w:e2e4")]
    [InlineData(GameKind.AliceChess, "b:e7e8q")]
    [InlineData(GameKind.HandAndBrain, "w:select=n")]
    [InlineData(GameKind.CardChess, "w:reroll=Seven+King")]
    [InlineData(GameKind.CardChess, "w:reroll=")]
    [InlineData(GameKind.ArcaneChess, "w:cast=Swap@e2+d2")]
    [InlineData(GameKind.ArcaneChess, "b:cast=SnapSwap#Seven")]
    [InlineData(GameKind.ArcaneChess, "w:cast=Peek")]
    [InlineData(GameKind.BiddingChess, "b:bid=12")]
    [InlineData(GameKind.DraftChess, "w:pick=q")]
    [InlineData(GameKind.DraftChess, "b:pass")]
    [InlineData(GameKind.DraftChess, "w:place=k@e1")]
    [InlineData(GameKind.DraftChess, "w:unplace=e1")]
    public void Action_RoundTrips(GameKind kind, string token)
    {
        Assert.Equal(token, ActionNotation.Parse(token, kind).ToString());
    }

    [Fact]
    public void HandAndBrainActions_GoToTheRightSeat()
    {
        Assert.Equal(SeatRole.Brain, ActionNotation.Parse("w:select=n", GameKind.HandAndBrain).Seat.Role);
        Assert.Equal(SeatRole.Hand, ActionNotation.Parse("w:g1f3", GameKind.HandAndBrain).Seat.Role);
    }

    [Theory]
    [InlineData("e2e4")]
    [InlineData("x:e2e4")]
    [InlineData("w:e2")]
    [InlineData("w:pick=z")]
    public void MalformedAction_IsRejected(string token)
    {
        Assert.Throws<FormatException>(() => ActionNotation.Parse(token, GameKind.AliceChess));
    }

    [Fact]
    public void PositionLine_RoundTripsWithItsSeed()
    {
        var line = PositionLine.Parse("seed=7 w:e2e4 b:e7e5");

        Assert.Equal(7, line.Seed);
        Assert.Equal(["w:e2e4", "b:e7e5"], line.Actions);
        Assert.Equal("seed=7 w:e2e4 b:e7e5", line.ToString());
    }

    [Fact]
    public void UnplayablePosition_NamesTheFailingAction()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            CoreGames.Start(GameKind.AliceChess, PositionLine.Parse("seed=0 w:e2e5")));

        Assert.Contains("w:e2e5", error.Message);
    }

    [Theory]
    [InlineData("martian-chess", GameKind.MartianChess)]
    [InlineData("MartianChess", GameKind.MartianChess)]
    [InlineData("hand-and-brain", GameKind.HandAndBrain)]
    public void Modes_ParseFolderAndEnumNames(string text, GameKind kind)
    {
        Assert.Equal(kind, Modes.Parse(text));
    }
}
