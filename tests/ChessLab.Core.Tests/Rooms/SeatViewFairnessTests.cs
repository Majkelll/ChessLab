using System.Text.Json;
using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Rooms;

public class SeatViewFairnessTests
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);
    private static readonly SeatId Black = new(Side.Black, SeatRole.Player);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly CardRank[] WhiteCards =
        [CardRank.Two, CardRank.Three, CardRank.Four, CardRank.Five, CardRank.Six, CardRank.Seven,
         CardRank.Eight, CardRank.Nine, CardRank.Ten, CardRank.Jack, CardRank.Queen, CardRank.King, CardRank.Ace];

    private static readonly SpellRank[] WhiteSpells = [SpellRank.Shield, SpellRank.FreezeSquare, SpellRank.Peek];

    private static Room BotWhiteAgainstHumanBlack(GameKind kind)
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), kind);
        room.SetBot(White, BotDifficulty.Easy);
        room.ClaimSeat(Black, Guid.NewGuid(), "Bob");
        return room;
    }

    private static string Serialized(GameStateEnvelopeDto state) =>
        JsonSerializer.Serialize(state, BotServiceJson.Options);

    private static ArcaneChessSession ArcaneGameWithOpponentHands(SpellRank[] blackSpells, CardRank[] blackCards)
    {
        var session = new ArcaneChessSession(BotWhiteAgainstHumanBlack(GameKind.ArcaneChess));
        session.StartWith(() => new ChessLab.Core.ArcaneChess.GameState(
            new ChessLab.Core.CardChess.GameState(new GeraChessRulesEngine(), new Clock(TimeSpan.FromMinutes(10), TimeSpan.Zero),
                new Deck(WhiteCards, new GameRandom(1)), new Deck(blackCards, new GameRandom(1))),
            new SpellDeck(WhiteSpells, new GameRandom(1)), new SpellDeck(blackSpells, new GameRandom(1)), new GameRandom(1)), Start);
        return session;
    }

    [Fact]
    public void ArcaneChess_TwoGamesDifferingOnlyInTheOpponentsHands_LookTheSameToTheBotsSeat()
    {
        var first = ArcaneGameWithOpponentHands(
            [SpellRank.Peek, SpellRank.Jam, SpellRank.Shield],
            [CardRank.Two, CardRank.Three, CardRank.Four, CardRank.Five, CardRank.Six,
             CardRank.Seven, CardRank.Eight, CardRank.Nine, CardRank.Ten, CardRank.Jack, CardRank.Queen, CardRank.King, CardRank.Ace]);
        var second = ArcaneGameWithOpponentHands(
            [SpellRank.Dispel, SpellRank.Disarm, SpellRank.Restoration],
            [CardRank.Ace, CardRank.King, CardRank.Queen, CardRank.Jack, CardRank.Ten,
             CardRank.Nine, CardRank.Eight, CardRank.Seven, CardRank.Six, CardRank.Five, CardRank.Four, CardRank.Three, CardRank.Two]);

        Assert.NotEqual(first.Game!.SpellHandOf(Side.Black), second.Game!.SpellHandOf(Side.Black));
        Assert.NotEqual(first.Game.HandOf(Side.Black), second.Game.HandOf(Side.Black));
        Assert.Equal(Serialized(first.ToStateDto(Side.White)), Serialized(second.ToStateDto(Side.White)));
    }

    [Fact]
    public void BiddingChess_TwoRoundsDifferingOnlyInTheOpponentsUnrevealedBid_LookTheSameToTheBotsSeat()
    {
        var low = new BiddingChessSession(BotWhiteAgainstHumanBlack(GameKind.BiddingChess));
        low.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start);
        low.Apply(GameAction.SubmitBid(3), Black, Start.AddSeconds(2));

        var high = new BiddingChessSession(BotWhiteAgainstHumanBlack(GameKind.BiddingChess));
        high.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start);
        high.Apply(GameAction.SubmitBid(77), Black, Start.AddSeconds(2));

        Assert.Equal(Serialized(low.ToStateDto(Side.White)), Serialized(high.ToStateDto(Side.White)));
    }
}
