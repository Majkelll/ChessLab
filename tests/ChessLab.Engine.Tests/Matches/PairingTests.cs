using ChessLab.Arena.Matches;
using ChessLab.Arena.Notation;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Matches;

public class PairingTests
{
    [Fact]
    public async Task CardChessPair_DealsTheSameDecksAndSwapsColours()
    {
        var candidateRequests = new List<BotActionRequest>();
        var baselineRequests = new List<BotActionRequest>();
        var settings = new MatchSettings(GameKind.CardChess, Recording("candidate", candidateRequests),
            Recording("baseline", baselineRequests), [PositionLine.Start()], Pairs: 1, Concurrency: 1);

        await MatchRunner.PlayPairAsync(settings, 0, CancellationToken.None);

        var candidateSides = candidateRequests.Select(request => request.Seat.Side).Distinct().ToArray();
        Assert.Equal([Side.White, Side.Black], candidateSides);

        var firstGameWhite = candidateRequests.First(request => request.Seat.Side == Side.White).State.CardChess!;
        var secondGameWhite = baselineRequests.First(request => request.Seat.Side == Side.White).State.CardChess!;
        Assert.Equal(firstGameWhite.WhiteHand, secondGameWhite.WhiteHand);
        Assert.Equal(firstGameWhite.BlackHand, secondGameWhite.BlackHand);
    }

    [Fact]
    public async Task CardChessPairsWithDifferentSeeds_DealDifferentDecks()
    {
        var hands = new List<string>();
        for (var index = 0; index < 6; index++)
        {
            var requests = new List<BotActionRequest>();
            var settings = new MatchSettings(GameKind.CardChess, Recording("candidate", requests),
                FakePlayer.First(), [PositionLine.Start()], Pairs: 6, Concurrency: 1);
            await MatchRunner.PlayPairAsync(settings, index, CancellationToken.None);
            hands.Add(string.Join(',', requests[0].State.CardChess!.WhiteHand));
        }

        Assert.True(hands.Distinct().Count() > 1);
    }

    [Fact]
    public async Task ExhaustedSuite_StartsAgainFromTheFirstOpeningWithNewSeeds()
    {
        PositionLine[] openings = [PositionLine.Parse("seed=0 w:e2e4"), PositionLine.Parse("seed=0 w:d2d4")];
        var settings = new MatchSettings(GameKind.AliceChess, FakePlayer.First(), FakePlayer.First(), openings,
            Pairs: 5, Concurrency: 1);

        var report = await MatchRunner.RunAsync(settings);

        var pairs = report.Pairs.OrderBy(pair => pair.Index).ToArray();
        Assert.Equal(["seed=0 w:e2e4", "seed=0 w:d2d4", "seed=0 w:e2e4", "seed=0 w:d2d4", "seed=0 w:e2e4"],
            pairs.Select(pair => pair.Opening));
        Assert.Equal(5, pairs.Select(pair => pair.Seed).Distinct().Count());
    }

    private static FakePlayer Recording(string name, List<BotActionRequest> requests) => new(name, (request, _) =>
    {
        requests.Add(request);
        var move = request.State.AvailableMoves[0];
        return Task.FromResult(GameAction.MovePiece(move.From, move.To, move.PromoteTo));
    });
}
