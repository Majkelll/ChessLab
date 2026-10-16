using ChessLab.Arena.Matches;
using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Matches;

public class MatchRunnerTests
{
    private static readonly PositionLine[] StartOnly = [PositionLine.Start()];

    public static TheoryData<string, Forfeit> BrokenPlayers => new()
    {
        { "illegal", Forfeit.IllegalAction },
        { "crashing", Forfeit.Crash },
        { "slow", Forfeit.TimeOverrun },
        { "hanging", Forfeit.TimeOverrun },
    };

    [Theory]
    [MemberData(nameof(BrokenPlayers))]
    public async Task BrokenBot_LosesEveryGameAndTheReportCountsIt(string broken, Forfeit forfeit)
    {
        var candidate = Broken(broken);
        var settings = new MatchSettings(GameKind.MartianChess, candidate, FakePlayer.First(), StartOnly, Pairs: 2,
            Concurrency: 2);

        var report = await MatchRunner.RunAsync(settings);

        Assert.Equal(4, report.Losses);
        Assert.Equal(4, report.ForfeitsOf(candidate.Name, forfeit));
        Assert.Equal(2, report.Pentanomial[0]);
        Assert.All(report.Pairs, pair => Assert.Equal(GameRecord.Describe(forfeit), pair.CandidateWhite.Reason));
        Assert.Contains($"Losses by {GameRecord.Describe(forfeit)} for {candidate.Name}: 4", report.Describe());
    }

    [Fact]
    public async Task EachSeat_SeesOnlyTheStateBuiltForIt()
    {
        var seen = new List<BotActionRequest>();
        var spy = new FakePlayer("spy", (request, _) =>
        {
            lock (seen)
                seen.Add(request);

            var move = request.State.AvailableMoves[0];
            return Task.FromResult(Core.Games.GameAction.MovePiece(move.From, move.To, move.PromoteTo));
        });

        var record = await GameRunner.PlayAsync(GameKind.ArcaneChess, PositionLine.Start(), 3, spy, spy, CancellationToken.None);

        Assert.NotNull(record.Reason);
        Assert.All(seen, request =>
        {
            var hidden = request.Seat.Side == Side.White
                ? request.State.Arcane!.BlackSpellHand
                : request.State.Arcane!.WhiteSpellHand;
            Assert.Empty(hidden);
        });
    }

    [Fact]
    public async Task CoreDecidesTheResult()
    {
        var random = new RandomPlayerFactory(new ArenaSettings());

        var record = await GameRunner.PlayAsync(GameKind.MartianChess, PositionLine.Start(), 5, random, random,
            CancellationToken.None);

        Assert.Null(record.Forfeit);
        Assert.Contains(record.Reason, Enum.GetNames<GameEndReason>().Append("action limit"));
    }

    private static FakePlayer Broken(string name) => name switch
    {
        "illegal" => FakePlayer.Illegal(),
        "crashing" => FakePlayer.Crashing(),
        "slow" => FakePlayer.Slow(),
        "hanging" => FakePlayer.Hanging(),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };
}
