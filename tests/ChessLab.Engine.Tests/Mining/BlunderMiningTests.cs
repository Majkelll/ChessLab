using ChessLab.Arena.Cli;
using ChessLab.Arena.Mining;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Refereeing;
using ChessLab.Arena.Suites;
using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using ChessLab.Data;

namespace ChessLab.Engine.Tests.Mining;

public class BlunderMiningTests
{
    [Fact]
    public void Replay_RecoversTheActionsBehindRecordedHandAndBrainMoves()
    {
        var line = PositionLine.Parse("seed=0 w:select=p w:e2e4 b:select=n b:g8f6 w:select=b w:f1c4");
        var notations = CoreGames.Start(GameKind.HandAndBrain, line).Game!.MoveNotations;

        var turns = GameReplay.Replay(GameKind.HandAndBrain, notations);

        Assert.Equal([["w:select=p", "w:e2e4"], ["b:select=n", "b:g8f6"], ["w:select=b", "w:f1c4"]],
            turns.Select(turn => turn.Played));
    }

    [Fact]
    public async Task StoredGameWithAKnownBlunder_PutsThePositionOnTheReviewList()
    {
        var (tactic, quiet) = MartianBlunder();
        var directory = Directory.CreateTempSubdirectory("mining");
        try
        {
            var connection = $"Data Source={Path.Combine(directory.FullName, "games.db")}";
            await StoreGameAsync(connection, tactic.Position.Then(quiet));
            var review = Path.Combine(directory.FullName, "review");

            var output = new StringWriter();
            var exitCode = await ArenaCli.RunAsync(["mine", "--connection", connection, "--days", "1", "--out", review],
                output, output);

            Assert.Equal(0, exitCode);
            var listed = await File.ReadAllLinesAsync(Path.Combine(review, "martian-chess", "review.txt"));
            Assert.Contains(tactic.ToString(), listed);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            directory.Delete(recursive: true);
        }
    }

    [SkippableFact]
    public async Task StoredPostgreSqlGameWithAKnownBlunder_PutsThePositionOnTheReviewList()
    {
        var connection = Environment.GetEnvironmentVariable("CHESSLAB_TEST_POSTGRES");
        Skip.If(string.IsNullOrWhiteSpace(connection), "Set CHESSLAB_TEST_POSTGRES to a PostgreSQL connection string.");

        var (tactic, quiet) = MartianBlunder();
        var review = Directory.CreateTempSubdirectory("mining-pg");
        try
        {
            await using (var db = GameRecords.Open(connection))
                await db.Database.EnsureDeletedAsync();
            await StoreGameAsync(connection, tactic.Position.Then(quiet));

            var output = new StringWriter();
            var exitCode = await ArenaCli.RunAsync(["mine", "--connection", connection, "--days", "1", "--out", review.FullName],
                output, output);

            Assert.Equal(0, exitCode);
            Assert.Contains(tactic.ToString(), await File.ReadAllLinesAsync(Path.Combine(review.FullName, "martian-chess", "review.txt")));
        }
        finally
        {
            review.Delete(recursive: true);
        }
    }

    private static (TacticPosition Tactic, string Quiet) MartianBlunder()
    {
        var tactic = EngineData.Locate(null).Lines(GameKind.MartianChess, "tactics.txt").Select(TacticPosition.Parse)
            .First(candidate => candidate.Expected.StartsWith("w:", StringComparison.Ordinal));
        var session = CoreGames.Start(GameKind.MartianChess, tactic.Position);
        var quiet = CoreGames.LegalActions(session).Select(ActionNotation.Format)
            .First(action => action != tactic.Expected && BlunderLoss(tactic, action) >= 200);
        return (tactic, quiet);
    }

    private static int BlunderLoss(TacticPosition tactic, string action)
    {
        var turns = new[] { new ReplayedTurn(Side.White, tactic.Position, [action]) };
        return BlunderMiner.Find(GameKind.MartianChess, turns, new HashSet<Side> { Side.White }, new MiningSettings())
            .Select(blunder => blunder.Loss).DefaultIfEmpty(0).Single();
    }

    private static async Task StoreGameAsync(string connection, PositionLine played)
    {
        var game = CoreGames.Start(GameKind.MartianChess, played).Game!;
        await using var db = GameRecords.Open(connection);
        await db.Database.EnsureCreatedAsync();
        db.GameRecords.Add(new GameRecord
        {
            Id = Guid.NewGuid(),
            RoomCode = "MINE01",
            Kind = GameKind.MartianChess,
            FinishedAtUtc = DateTime.UtcNow,
            EndReason = GameEndReason.Resignation,
            Winner = Side.Black,
            MoveCount = game.MoveNotations.Count,
            MovesSan = string.Join(' ', game.MoveNotations),
            FinalFen = game.PositionText,
            Players =
            [
                new GameRecordPlayer { Id = Guid.NewGuid(), Side = Side.White, Role = SeatRole.Player, BotDifficulty = BotDifficulty.Hard },
                new GameRecordPlayer { Id = Guid.NewGuid(), Side = Side.Black, Role = SeatRole.Player, UserId = Guid.NewGuid(), DisplayName = "Human" },
            ],
        });
        await db.SaveChangesAsync();
    }
}
