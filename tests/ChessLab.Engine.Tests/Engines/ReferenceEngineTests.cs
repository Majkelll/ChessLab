using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Engines;

public class ReferenceEngineTests
{
    public static TheoryData<GameKind> AllModes => [.. Modes.All];

    [Theory]
    [MemberData(nameof(AllModes))]
    public void LegalActions_AreTheActionsCoreMakesAvailable(GameKind kind)
    {
        var engine = new CoreReferenceEngine(kind);
        var position = PositionLine.Start(3);
        engine.Load(position);

        var core = CoreGames.LegalActions(CoreGames.Start(kind, position)).Select(ActionNotation.Format);

        Assert.Equal(core.Order(), engine.LegalActions().Order());
        Assert.NotEmpty(engine.LegalActions());
    }

    [Theory]
    [MemberData(nameof(AllModes))]
    public void ApplyAndUndo_RestoreThePositionAndHash(GameKind kind)
    {
        var engine = new CoreReferenceEngine(kind);
        engine.Load(PositionLine.Start(3));
        var (text, hash) = (engine.PositionText, engine.Hash);

        engine.Apply(engine.LegalActions()[0]);
        Assert.NotEqual(hash, engine.Hash);

        engine.Undo();
        Assert.Equal((text, hash), (engine.PositionText, engine.Hash));
    }

    [Theory]
    [InlineData(GameKind.MartianChess)]
    [InlineData(GameKind.AliceChess)]
    [InlineData(GameKind.HandAndBrain)]
    public void Search_CountsNodesAndReturnsALegalAction(GameKind kind)
    {
        var engine = new CoreReferenceEngine(kind);
        engine.Load(PositionLine.Start());

        var result = engine.Search(2);

        Assert.True(result.Nodes > 1);
        Assert.Contains(result.BestAction, engine.LegalActions());
    }

    [Fact]
    public void Search_FindsAFreeCapture()
    {
        var engine = new CoreReferenceEngine(GameKind.AbsorptionChess);
        engine.Load(PositionLine.Parse("seed=0 w:e2e4 b:d7d5"));

        Assert.Equal("w:e4d5", engine.Search(1).BestAction);
    }
}
