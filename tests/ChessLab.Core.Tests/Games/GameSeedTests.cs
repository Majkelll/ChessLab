using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Games;

public class GameSeedTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(GameKind.CardChess)]
    [InlineData(GameKind.ArcaneChess)]
    public void EqualSeeds_DealEqualDecksThroughoutTheGame(GameKind kind)
    {
        var first = StartedSession(kind, 42);
        var second = StartedSession(kind, 42);

        for (var ply = 0; ply < 40 && first.HasActiveGame; ply++)
        {
            Assert.Equal(DealtCards(first), DealtCards(second));

            var seat = first.ActiveSeats[0];
            var move = first.ToStateDto(seat.Side).AvailableMoves[0];
            first.Apply(GameActionOf(move), seat, Start);
            second.Apply(GameActionOf(move), seat, Start);
        }

        Assert.Equal(DealtCards(first), DealtCards(second));
    }

    [Theory]
    [InlineData(GameKind.CardChess)]
    [InlineData(GameKind.ArcaneChess)]
    public void DifferentSeeds_DealDifferentDecks(GameKind kind)
    {
        var deals = Enumerable.Range(0, 10).Select(seed => DealtCards(StartedSession(kind, seed))).ToArray();

        Assert.True(deals.Distinct().Count() > 1);
    }

    [Theory]
    [InlineData(GameKind.CardChess)]
    [InlineData(GameKind.ArcaneChess)]
    public void UnseededGames_Differ(GameKind kind)
    {
        var deals = Enumerable.Range(0, 10).Select(_ => DealtCards(StartedSession(kind, null))).ToArray();

        Assert.True(deals.Distinct().Count() > 1);
    }

    [Theory]
    [InlineData(GameKind.CardChess)]
    [InlineData(GameKind.ArcaneChess)]
    public void Reseed_MakesLaterDrawsFollowTheNewSeed(GameKind kind)
    {
        var draws = new[] { 9, 9, 10 }.Select(newSeed =>
        {
            var session = StartedSession(kind, 1);
            PlayFirstMoves(session, 10);
            return DrawsAfter(session, newSeed);
        }).ToArray();

        Assert.Equal(draws[0], draws[1]);
        Assert.NotEqual(draws[0], draws[2]);
    }

    private static void PlayFirstMoves(IRoomSession session, int plies)
    {
        for (var ply = 0; ply < plies && session.HasActiveGame; ply++)
        {
            var seat = session.ActiveSeats[0];
            session.Apply(GameActionOf(session.ToStateDto(seat.Side).AvailableMoves[0]), seat, Start);
        }
    }

    private static void Reseed(IRoomSession session, int seed)
    {
        switch (session.Game)
        {
            case Core.CardChess.GameState card:
                card.Reseed(seed);
                break;
            case Core.ArcaneChess.GameState arcane:
                arcane.Reseed(seed);
                break;
        }
    }

    private static string DrawsAfter(IRoomSession session, int seed)
    {
        Reseed(session, seed);
        PlayFirstMoves(session, 10);
        return DealtCards(session);
    }

    private static IRoomSession StartedSession(GameKind kind, int? seed)
    {
        var room = new Room("SEED", Guid.NewGuid(), kind);
        room.ClaimSeat(new SeatId(Side.White, SeatRole.Player), Guid.NewGuid(), "White");
        room.ClaimSeat(new SeatId(Side.Black, SeatRole.Player), Guid.NewGuid(), "Black");
        IRoomSession session = kind == GameKind.CardChess ? new CardChessSession(room) : new ArcaneChessSession(room);
        session.Start(TimeSpan.FromHours(1), TimeSpan.Zero, Start, seed);
        return session;
    }

    private static string DealtCards(IRoomSession session)
    {
        var state = session.ToStateDto(null);
        var cards = session.Game switch
        {
            Core.CardChess.GameState card => new[] { card.HandOf(Side.White), card.HandOf(Side.Black) }
                .Select(hand => string.Join(',', hand)),
            Core.ArcaneChess.GameState arcane => new[]
            {
                string.Join(',', arcane.HandOf(Side.White)),
                string.Join(',', arcane.HandOf(Side.Black)),
                string.Join(',', arcane.SpellHandOf(Side.White)),
                string.Join(',', arcane.SpellHandOf(Side.Black)),
            },
            _ => throw new InvalidOperationException("Only card modes deal cards."),
        };

        return $"{state.PositionText} {string.Join(" | ", cards)}";
    }

    private static Core.Games.GameAction GameActionOf(ChessMove move) =>
        Core.Games.GameAction.MovePiece(move.From, move.To, move.PromoteTo);
}
