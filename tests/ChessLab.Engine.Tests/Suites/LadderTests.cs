using ChessLab.Arena.Matches;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Suites;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Suites;

public class LadderTests
{
    [Fact]
    public void BaselineLadder_HasARowForEveryMode()
    {
        var path = Path.Combine(EngineData.Locate(null).Root, "ladder.csv");
        var lines = File.ReadAllLines(path);

        Assert.Equal(Ladder.Header, lines[0]);
        var modes = lines.Skip(1).Select(line => line.Split(',')[1]).ToHashSet();
        Assert.All(Modes.All, kind => Assert.Contains(Modes.FolderOf(kind), modes));
    }

    [Fact]
    public async Task Row_RecordsTheMatchAndItsElo()
    {
        var match = await MatchRunner.RunAsync(new MatchSettings(GameKind.MartianChess,
            Matches.FakePlayer.First(), Matches.FakePlayer.Illegal(), [PositionLine.Start()], Pairs: 2, Concurrency: 1));

        var row = Ladder.Row(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero), "abc123", match).Split(',');

        Assert.Equal(["2026-10-15", "martian-chess", "abc123", "first", "illegal", "4", "1.0000"], row[..7]);
    }
}
