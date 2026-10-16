using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Arena.Suites;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Suites;

public class OpeningSuiteTests
{
    public static TheoryData<GameKind> AllModes => [.. Modes.All];

    [Theory]
    [MemberData(nameof(AllModes))]
    public void EveryOpening_LoadsInCoreAndIsNotFinished(GameKind kind)
    {
        var openings = EngineData.Locate(null).Openings(kind);

        Assert.True(openings.Count >= 50, $"{Modes.FolderOf(kind)} has only {openings.Count} openings.");
        Assert.All(openings, opening => Assert.True(CoreGames.Start(kind, opening).HasActiveGame, opening.ToString()));
        Assert.Equal(openings.Count, openings.Select(opening => opening.ToString()).Distinct().Count());
    }
}
