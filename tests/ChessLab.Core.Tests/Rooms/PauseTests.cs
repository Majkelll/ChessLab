using ChessLab.Core.Chess;
using ChessLab.Core.Games;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Rooms;

public class PauseTests
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);
    private static readonly SeatId Black = new(Side.Black, SeatRole.Player);
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);

    private static Room HumanWhiteAgainstBotBlack(GameKind kind)
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), kind);
        room.ClaimSeat(White, Guid.NewGuid(), "Ala");
        room.SetBot(Black, BotDifficulty.Easy);
        return room;
    }

    private static Room BotWhiteAgainstHumanBlack(GameKind kind)
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), kind);
        room.SetBot(White, BotDifficulty.Easy);
        room.ClaimSeat(Black, Guid.NewGuid(), "Bob");
        return room;
    }

    private static IRoomSession NewSession(GameKind kind, Room room) => kind switch
    {
        GameKind.CardChess => new CardChessSession(room),
        GameKind.ArcaneChess => new ArcaneChessSession(room),
        GameKind.BiddingChess => new BiddingChessSession(room),
        GameKind.ProgressiveChess => new ProgressiveChessSession(room),
        GameKind.AliceChess => new AliceChessSession(room),
        GameKind.AbsorptionChess => new AbsorptionChessSession(room),
        GameKind.MartianChess => new MartianChessSession(room),
        GameKind.DraftChess => new DraftChessSession(room),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Two-seat modes only."),
    };

    private static IRoomSession StartedAliceGame(TimeSpan? clock = null)
    {
        var session = new AliceChessSession(HumanWhiteAgainstBotBlack(GameKind.AliceChess));
        session.Start(clock ?? TenMinutes, TimeSpan.Zero, Start);
        return session;
    }

    private static GameAction FirstMoveFor(IRoomSession session, Side side)
    {
        var move = session.ToStateDto(side).AvailableMoves[0];
        return GameAction.MovePiece(move.From, move.To, move.PromoteTo);
    }

    [Fact]
    public void Apply_WhilePaused_IsRejectedWithThePauseMessage()
    {
        var session = StartedAliceGame();
        session.Pause(Start);

        var rejection = Assert.Throws<InvalidOperationException>(() =>
            session.Apply(FirstMoveFor(session, Side.White), White, Start.AddSeconds(1)));

        Assert.Equal("The game is paused because a bot is unavailable.", rejection.Message);
    }

    [Fact]
    public void Resign_WhilePaused_EndsTheGameByResignationAndDropsTheDeadline()
    {
        var session = StartedAliceGame();
        session.Pause(Start);

        session.Game!.Resign(Side.White);

        Assert.Equal(new GameEndResult(GameEndReason.Resignation, Side.Black), session.Game.EndResult);
        Assert.Null(session.ToStateDto(Side.White).PauseDeadline);
    }

    [Fact]
    public void PauseDeadline_IsFiveMinutesAfterThePauseInEveryStateAndUpdate()
    {
        var session = StartedAliceGame();
        session.Pause(Start);

        Assert.Equal(Start.AddMinutes(5), session.ToStateDto(Side.White).PauseDeadline);
        Assert.Equal(Start.AddMinutes(5), session.ToUpdateDto(Side.Black).PauseDeadline);
        Assert.Equal(Start.AddMinutes(5), session.ToUpdateDto(null).PauseDeadline);
    }

    [Fact]
    public void PauseDeadline_IsAbsentWhileTheGameRuns()
    {
        var session = StartedAliceGame();

        Assert.Null(session.ToStateDto(Side.White).PauseDeadline);
        Assert.Null(session.ToUpdateDto(Side.White).PauseDeadline);
    }

    [Fact]
    public void Pause_WhenAlreadyPaused_KeepsTheFirstDeadline()
    {
        var session = StartedAliceGame();
        session.Pause(Start);

        session.Pause(Start.AddMinutes(2));

        Assert.Equal(Start.AddMinutes(5), session.PauseDeadline);
    }

    [Fact]
    public void ApplyBotAction_ResumesAPausedGame()
    {
        var session = new AliceChessSession(BotWhiteAgainstHumanBlack(GameKind.AliceChess));
        session.Start(TenMinutes, TimeSpan.Zero, Start);
        session.Pause(Start);

        session.ApplyBotAction(FirstMoveFor(session, Side.White), White, Start.AddMinutes(1), Start.AddMinutes(1));

        Assert.False(session.IsPaused);
        Assert.Null(session.PauseDeadline);
        Assert.Single(session.Game!.MoveHistory);
    }

    [Fact]
    public void ApplyBotAction_WhenTheRulesRejectIt_KeepsTheGamePausedWithItsTimers()
    {
        var session = new AliceChessSession(BotWhiteAgainstHumanBlack(GameKind.AliceChess));
        session.Start(TenMinutes, TimeSpan.Zero, Start);
        session.Pause(Start.AddSeconds(3));

        Assert.Throws<InvalidOperationException>(() => session.ApplyBotAction(
            GameAction.MovePiece(Square.Parse("e2"), Square.Parse("e5")), White, Start.AddMinutes(1), Start.AddMinutes(1)));
        session.ApplyBotAction(FirstMoveFor(session, Side.White), White, Start.AddSeconds(3).AddMinutes(2), Start.AddSeconds(3).AddMinutes(2).AddSeconds(1));

        Assert.Equal(TenMinutes - TimeSpan.FromSeconds(4), session.Game!.Clock.WhiteRemaining);
    }

    [Theory]
    [InlineData(GameKind.CardChess)]
    [InlineData(GameKind.ArcaneChess)]
    [InlineData(GameKind.ProgressiveChess)]
    [InlineData(GameKind.AliceChess)]
    [InlineData(GameKind.AbsorptionChess)]
    [InlineData(GameKind.MartianChess)]
    public void Resume_DeductsOnlyTheTimeBeforeAndAfterThePause(GameKind kind)
    {
        var session = NewSession(kind, BotWhiteAgainstHumanBlack(kind));
        session.Start(TenMinutes, TimeSpan.Zero, Start, new Random(3));
        session.Pause(Start.AddSeconds(3));

        session.ApplyBotAction(FirstMoveFor(session, Side.White), White, Start.AddSeconds(3).AddMinutes(2), Start.AddSeconds(3).AddMinutes(2).AddSeconds(1));

        Assert.Equal(TenMinutes - TimeSpan.FromSeconds(4), session.Game!.Clock.WhiteRemaining);
    }

    [Fact]
    public void Resume_InHandAndBrain_DeductsOnlyTheTimeBeforeAndAfterThePause()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        room.SetBot(new SeatId(Side.White, SeatRole.Brain), BotDifficulty.Easy);
        room.SetBot(new SeatId(Side.White, SeatRole.Hand), BotDifficulty.Easy);
        room.ClaimSeat(new SeatId(Side.Black, SeatRole.Brain), Guid.NewGuid(), "Cai");
        room.ClaimSeat(new SeatId(Side.Black, SeatRole.Hand), Guid.NewGuid(), "Deb");
        var session = new GameSession(room);
        session.Start(TenMinutes, TimeSpan.Zero, Start);
        session.Apply(GameAction.SelectPieceKind(PieceKind.Pawn), new SeatId(Side.White, SeatRole.Brain), Start.AddSeconds(1));
        session.Pause(Start.AddSeconds(3));

        session.ApplyBotAction(FirstMoveFor(session, Side.White), new SeatId(Side.White, SeatRole.Hand),
            Start.AddSeconds(3).AddMinutes(2), Start.AddSeconds(3).AddMinutes(2).AddSeconds(1));

        Assert.Equal(TurnPhase.BrainSelecting, session.Game!.Phase);
        Assert.Equal(TenMinutes - TimeSpan.FromSeconds(4), session.Game.Clock.WhiteRemaining);
    }

    [Fact]
    public void Resume_InBiddingChess_DeductsOnlyTheTimeBeforeAndAfterThePauseFromABid()
    {
        var session = new BiddingChessSession(BotWhiteAgainstHumanBlack(GameKind.BiddingChess));
        session.Start(TenMinutes, TimeSpan.Zero, Start);
        session.Apply(GameAction.SubmitBid(5), Black, Start.AddSeconds(2));
        session.Pause(Start.AddSeconds(3));

        session.ApplyBotAction(GameAction.SubmitBid(10), White, Start.AddSeconds(3).AddMinutes(2), Start.AddSeconds(3).AddMinutes(2).AddSeconds(1));

        Assert.Equal(TenMinutes - TimeSpan.FromSeconds(4), session.Game!.Clock.WhiteRemaining);
        Assert.Equal(TenMinutes - TimeSpan.FromSeconds(2), session.Game.Clock.BlackRemaining);
    }

    [Fact]
    public void Resume_InBiddingChess_DeductsOnlyTheTimeBeforeAndAfterThePauseFromAMove()
    {
        var session = new BiddingChessSession(BotWhiteAgainstHumanBlack(GameKind.BiddingChess));
        session.Start(TenMinutes, TimeSpan.Zero, Start);
        session.Apply(GameAction.SubmitBid(0), Black, Start);
        session.ApplyBotAction(GameAction.SubmitBid(10), White, Start, Start);
        session.Pause(Start.AddSeconds(3));

        session.ApplyBotAction(FirstMoveFor(session, Side.White), White, Start.AddSeconds(3).AddMinutes(2), Start.AddSeconds(3).AddMinutes(2).AddSeconds(1));

        Assert.Equal(TenMinutes - TimeSpan.FromSeconds(4), session.Game!.Clock.WhiteRemaining);
    }

    [Fact]
    public void Resume_InDraftChess_DeductsOnlyTheTimeBeforeAndAfterThePauseFromAPick()
    {
        var session = new DraftChessSession(BotWhiteAgainstHumanBlack(GameKind.DraftChess));
        session.Start(TenMinutes, TimeSpan.Zero, Start);
        session.Pause(Start.AddSeconds(3));

        session.ApplyBotAction(GameAction.DraftPick(PieceKind.Queen), White, Start.AddSeconds(3).AddMinutes(2), Start.AddSeconds(3).AddMinutes(2).AddSeconds(1));

        Assert.Equal(TenMinutes - TimeSpan.FromSeconds(4), session.Game!.Clock.WhiteRemaining);
    }

    [Fact]
    public void Resume_InDraftChess_DeductsOnlyTheTimeBeforeAndAfterThePauseFromEachSidesPlacing()
    {
        var session = new DraftChessSession(BotWhiteAgainstHumanBlack(GameKind.DraftChess));
        session.Start(TenMinutes, TimeSpan.Zero, Start);
        session.ApplyBotAction(GameAction.DraftPass(), White, Start, Start);
        session.Apply(GameAction.DraftPass(), Black, Start);
        session.Pause(Start.AddSeconds(3));

        var resumedAt = Start.AddSeconds(3).AddMinutes(2);
        session.ApplyBotAction(GameAction.PlacePiece(PieceKind.King, Square.Parse("e1")), White, resumedAt, resumedAt.AddSeconds(1));
        session.Apply(GameAction.PlacePiece(PieceKind.King, Square.Parse("e8")), Black, resumedAt.AddSeconds(2));

        var withTimeBonus = TenMinutes + session.Game!.TimeBonusOf(Side.White);
        Assert.Equal(withTimeBonus - TimeSpan.FromSeconds(4), session.Game.Clock.WhiteRemaining);
        Assert.Equal(withTimeBonus - TimeSpan.FromSeconds(5), session.Game.Clock.BlackRemaining);
    }

    [Fact]
    public void DeclareTimeoutIfExpired_WhilePaused_DoesNotFlagASideWithTimeLeftAtThePause()
    {
        var session = StartedAliceGame(TimeSpan.FromSeconds(2));
        session.Pause(Start);

        session.DeclareTimeoutIfExpired(Start.AddMinutes(1));

        Assert.True(session.HasActiveGame);
    }

    [Fact]
    public void EndIfPauseExpired_BeforeTheDeadline_KeepsTheGamePaused()
    {
        var session = StartedAliceGame();
        session.Pause(Start);

        Assert.False(session.EndIfPauseExpired(Start.AddMinutes(5).AddSeconds(-1)));
        Assert.True(session.IsPaused);
    }

    [Fact]
    public void EndIfPauseExpired_HumanAgainstAnUnavailableBot_TheHumanWins()
    {
        var session = StartedAliceGame();
        session.Pause(Start);

        Assert.True(session.EndIfPauseExpired(Start.AddMinutes(5)));

        Assert.Equal(new GameEndResult(GameEndReason.BotUnavailable, Side.White), session.Game!.EndResult);
        Assert.Null(session.PauseDeadline);
    }

    [Fact]
    public void EndBecauseBotUnavailable_WithABotPartnerInHandAndBrain_TheOtherSideWins()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        room.ClaimSeat(new SeatId(Side.White, SeatRole.Brain), Guid.NewGuid(), "Ala");
        room.SetBot(new SeatId(Side.White, SeatRole.Hand), BotDifficulty.Easy);
        room.ClaimSeat(new SeatId(Side.Black, SeatRole.Brain), Guid.NewGuid(), "Cai");
        room.ClaimSeat(new SeatId(Side.Black, SeatRole.Hand), Guid.NewGuid(), "Deb");
        var session = new GameSession(room);
        session.Start(TenMinutes, TimeSpan.Zero, Start);

        session.EndBecauseBotUnavailable();

        Assert.Equal(new GameEndResult(GameEndReason.BotUnavailable, Side.Black), session.Game!.EndResult);
    }

    [Theory]
    [InlineData(GameKind.CardChess)]
    [InlineData(GameKind.ProgressiveChess)]
    [InlineData(GameKind.BiddingChess)]
    [InlineData(GameKind.DraftChess)]
    [InlineData(GameKind.MartianChess)]
    public void EndBecauseBotUnavailable_WithBotsOnBothSides_IsADraw(GameKind kind)
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), kind);
        room.SetBot(White, BotDifficulty.Easy);
        room.SetBot(Black, BotDifficulty.Hard);
        var session = NewSession(kind, room);
        session.Start(TenMinutes, TimeSpan.Zero, Start, new Random(3));

        session.EndBecauseBotUnavailable();

        Assert.Equal(new GameEndResult(GameEndReason.BotUnavailable, null), session.Game!.EndResult);
    }

    [Fact]
    public void Pause_AfterAnInterruptedPause_GetsAFullFiveMinutes()
    {
        var session = new AliceChessSession(BotWhiteAgainstHumanBlack(GameKind.AliceChess));
        session.Start(TenMinutes, TimeSpan.Zero, Start);
        session.Pause(Start);
        session.ApplyBotAction(FirstMoveFor(session, Side.White), White, Start.AddMinutes(4), Start.AddMinutes(4));
        session.Apply(FirstMoveFor(session, Side.Black), Black, Start.AddMinutes(4).AddSeconds(1));

        session.Pause(Start.AddMinutes(6));

        Assert.False(session.EndIfPauseExpired(Start.AddMinutes(10)));
        Assert.True(session.EndIfPauseExpired(Start.AddMinutes(11)));
    }
}
