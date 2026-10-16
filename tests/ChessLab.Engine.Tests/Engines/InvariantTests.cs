using ChessLab.Arena.Checks;
using ChessLab.Arena.Engines;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Suites;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Engines;

public class InvariantTests
{
    private static IReadOnlyList<PositionLine> Openings => [.. EngineData.Locate(null).Openings(GameKind.MartianChess).Take(6)];

    [Fact]
    public void SampleEngine_KeepsEveryInvariant()
    {
        var engine = new SampleMartianEngine();

        Assert.Empty(Invariants.Check(engine, Openings));
        Assert.Null(Invariants.CheckSearchAllocation(engine, Openings[0], 3));
    }

    [Theory]
    [InlineData(MartianBug.UndoKeepsScore, Invariants.MakeUnmake)]
    [InlineData(MartianBug.HashIgnoresScore, Invariants.IncrementalHash)]
    [InlineData(MartianBug.EvaluationFavoursWhite, Invariants.MirroredEvaluation)]
    public void SeededBug_BreaksItsInvariant(MartianBug bug, string invariant)
    {
        var violations = Invariants.Check(new SampleMartianEngine(bug), Openings, pliesPerPosition: 40);

        Assert.Contains(violations, violation => violation.Invariant == invariant);
    }

    [Fact]
    public void AllocationPerNode_IsCaught()
    {
        var violation = Invariants.CheckSearchAllocation(new SampleMartianEngine(MartianBug.AllocatesPerNode), Openings[0], 3);

        Assert.NotNull(violation);
        Assert.Equal(Invariants.SearchAllocation, violation.Invariant);
    }

    [Fact]
    public void ReferenceEngine_KeepsMakeUnmakeAndHash()
    {
        var violations = Invariants.Check(new CoreReferenceEngine(GameKind.AbsorptionChess),
            [PositionLine.Start()], pliesPerPosition: 4);

        Assert.Empty(violations);
    }
}
