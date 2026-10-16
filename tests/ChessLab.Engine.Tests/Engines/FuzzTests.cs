using ChessLab.Arena.Checks;
using ChessLab.Arena.Engines;
using ChessLab.Arena.Refereeing;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Engines;

public class FuzzTests
{
    [Fact]
    public void SampleEngine_AgreesWithCoreOverManyRandomGames()
    {
        for (var seed = 0; seed < 40; seed++)
            Assert.Null(DifferentialFuzz.Run(new SampleMartianEngine(), seed));
    }

    [Fact]
    public void ReferenceEngine_AgreesWithCore()
    {
        Assert.Null(DifferentialFuzz.Run(new CoreReferenceEngine(GameKind.BiddingChess), 1, actionLimit: 60));
    }

    [Fact]
    public void SeededBug_IsCaughtWithAReproduciblePositionAndSeed()
    {
        var failure = Enumerable.Range(0, 40)
            .Select(seed => DifferentialFuzz.Run(new SampleMartianEngine(MartianBug.NoLongDiagonals), seed))
            .FirstOrDefault(found => found is not null);

        Assert.NotNull(failure);
        Assert.NotEmpty(failure.MissingFromEngine);
        Assert.Equal(failure.PositionText, CoreGames.Start(GameKind.MartianChess, failure.Position).Game!.PositionText);
        Assert.Contains($"seed: {failure.Seed}", failure.Describe());
    }
}
