using ChessLab.Arena.Cli;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Arena.Suites;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Suites;

public class TacticsTests
{
    public static TheoryData<GameKind> AllModes => [.. Modes.All];

    [Theory]
    [MemberData(nameof(AllModes))]
    public void EveryTactic_LoadsAndItsExpectedActionIsAvailable(GameKind kind)
    {
        var suite = EngineData.Locate(null).Lines(kind, "tactics.txt").Select(TacticPosition.Parse).ToArray();

        Assert.True(suite.Length >= 10, $"{Modes.FolderOf(kind)} has only {suite.Length} tactics.");
        Assert.All(suite, tactic =>
        {
            var session = CoreGames.Start(kind, tactic.Position);
            Assert.True(session.HasActiveGame, tactic.ToString());
            Assert.Contains(tactic.Expected, CoreGames.LegalActions(session).Select(ActionNotation.Format));
        });
    }

    [Fact]
    public async Task EngineSolver_SolvesTheMartianSuite()
    {
        var suite = EngineData.Locate(null).Lines(GameKind.MartianChess, "tactics.txt").Select(TacticPosition.Parse).ToArray();

        var report = await Tactics.SolveAsync(GameKind.MartianChess, suite, new EngineSolver(2), CancellationToken.None);

        Assert.Equal(suite.Length, report.Solved.Count);
    }

    [Fact]
    public async Task PositionNoLongerSolved_FailsTheCheckAndIsListed()
    {
        var data = Directory.CreateTempSubdirectory("tactics");
        try
        {
            var mode = Directory.CreateDirectory(Path.Combine(data.FullName, "martian-chess"));
            var real = TacticPosition.Parse(EngineData.Locate(null).Lines(GameKind.MartianChess, "tactics.txt")[0]);
            var quietAction = CoreGames.LegalActions(CoreGames.Start(GameKind.MartianChess, real.Position))
                .Select(ActionNotation.Format).First(action => action != real.Expected);
            var tactic = real with { Expected = quietAction };
            await File.WriteAllTextAsync(Path.Combine(mode.FullName, "tactics.txt"), tactic + "\n");
            var solvedOnMain = Path.Combine(data.FullName, "main.txt");
            await File.WriteAllTextAsync(solvedOnMain, $"martian-chess {tactic}\n");

            var output = new StringWriter();
            var exitCode = await ArenaCli.RunAsync(
                ["tactics", "--mode", "martian-chess", "--player", "bot:Expert", "--data", data.FullName,
                 "--compare", solvedOnMain],
                output, output);

            Assert.Equal(1, exitCode);
            Assert.Contains("no longer solved", output.ToString());
        }
        finally
        {
            data.Delete(recursive: true);
        }
    }
}
