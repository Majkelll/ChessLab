using ChessLab.Arena.Checks;
using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Engines;

public class PerftTests
{
    [Fact]
    public void ReferencePerft_MatchesKnownChessCounts()
    {
        var engine = new CoreReferenceEngine(GameKind.ProgressiveChess);

        Assert.Equal(20, Perft.Count(engine, PositionLine.Start(), 1));
        Assert.Equal(400, Perft.Count(engine, PositionLine.Start(), 2));
    }

    [Fact]
    public void ReferenceEngineAgainstItself_HasNoDifference()
    {
        var mismatch = Perft.FirstDifference(new CoreReferenceEngine(GameKind.AliceChess),
            new CoreReferenceEngine(GameKind.AliceChess), PositionLine.Start(), 3);

        Assert.Null(mismatch);
    }

    [Fact]
    public void SeededBug_IsReportedWithItsPositionDepthAndFirstDifferingAction()
    {
        var buggy = new DroppingEngine(new CoreReferenceEngine(GameKind.AliceChess), action => action == "b:g8f6");

        var mismatch = Perft.FirstDifference(buggy, new CoreReferenceEngine(GameKind.AliceChess), PositionLine.Start(), 3);

        Assert.NotNull(mismatch);
        Assert.Equal(["b:g8f6"], mismatch.MissingFromEngine);
        Assert.Single(mismatch.Position.Actions);
        Assert.Equal(2, mismatch.Depth);
        Assert.Contains("b:g8f6", mismatch.Describe());
    }

    [Theory]
    [InlineData("seed=0")]
    [InlineData("seed=0 w:c3d2 b:b7a6 w:a3a4 b:c8b7 w:a4a3 b:d8c8")]
    public void SampleEngine_MatchesCore(string position)
    {
        var mismatch = Perft.FirstDifference(new SampleMartianEngine(), new CoreReferenceEngine(GameKind.MartianChess),
            PositionLine.Parse(position), 3);

        Assert.Null(mismatch);
    }

    [Fact]
    public void SampleEngineWithAMoveGenerationBug_IsCaught()
    {
        var mismatch = Perft.FirstDifference(new SampleMartianEngine(MartianBug.NoLongDiagonals),
            new CoreReferenceEngine(GameKind.MartianChess), PositionLine.Parse("seed=0 w:c3d2 b:b7a6"), 3);

        Assert.NotNull(mismatch);
        Assert.NotEmpty(mismatch.MissingFromEngine);
    }
}
