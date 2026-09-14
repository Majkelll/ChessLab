using ChessLab.Core.Chess;
using ChessLab.Core.HandBrain;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Rooms;

public class GameSessionTests
{
    private static readonly SeatId WhiteBrain = new(Side.White, SeatRole.Brain);
    private static readonly SeatId WhiteHand = new(Side.White, SeatRole.Hand);
    private static readonly SeatId BlackBrain = new(Side.Black, SeatRole.Brain);
    private static readonly SeatId BlackHand = new(Side.Black, SeatRole.Hand);

    private static GameSession NewFullSession()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        room.ClaimSeat(WhiteBrain, Guid.NewGuid(), "Ala");
        room.ClaimSeat(WhiteHand, Guid.NewGuid(), "Bob");
        room.ClaimSeat(BlackBrain, Guid.NewGuid(), "Cai");
        room.ClaimSeat(BlackHand, Guid.NewGuid(), "Deb");
        return new GameSession(room);
    }

    [Fact]
    public void Start_WhenRoomNotFull_Throws()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        var session = new GameSession(room);

        Assert.Throws<InvalidOperationException>(() => session.Start(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Start_LocksRoomAndCreatesGame()
    {
        var session = NewFullSession();

        session.Start(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5), DateTimeOffset.UtcNow);

        Assert.True(session.Room.IsLocked);
        Assert.NotNull(session.Game);
    }

    [Fact]
    public void ActiveSeat_BeforeStart_Throws()
    {
        var session = NewFullSession();

        Assert.Throws<InvalidOperationException>(() => session.ActiveSeat);
    }

    [Fact]
    public void ActiveSeat_AtStart_IsWhiteBrain()
    {
        var session = NewFullSession();
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5), DateTimeOffset.UtcNow);

        Assert.Equal(WhiteBrain, session.ActiveSeat);
    }

    [Fact]
    public void ActiveSeat_AfterBrainSelects_IsWhiteHand()
    {
        var session = NewFullSession();
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5), DateTimeOffset.UtcNow);

        session.Game!.SelectPieceKind(PieceKind.Pawn);

        Assert.Equal(WhiteHand, session.ActiveSeat);
    }

    [Fact]
    public void MakeMove_DeductsElapsedWallClockTime()
    {
        var session = NewFullSession();
        var start = DateTimeOffset.UtcNow;
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5), start);
        session.Game!.SelectPieceKind(PieceKind.Pawn);
        var move = session.Game.AvailableMoves().Single(m => m.From == Square.Parse("e2") && m.To == Square.Parse("e4"));

        session.MakeMove(move.From, move.To, move.PromoteTo, start + TimeSpan.FromSeconds(12));

        Assert.Equal(
            TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(12) + TimeSpan.FromSeconds(5),
            session.Game.Clock.WhiteRemaining);
        Assert.Equal(BlackBrain, session.ActiveSeat);
    }

    /// <summary>
    /// Regression coverage for a real bug: <see cref="Clock"/> only holds the remaining time as
    /// of the last move — nothing decrements it just from wall-clock time passing — so a side
    /// that simply stops playing (no resignation, no move, nothing) would otherwise never be
    /// flagged, no matter how long the background watchdog waited.
    /// </summary>
    [Fact]
    public void DeclareTimeoutIfExpired_WhenNoOneEverMoves_StillEndsTheGameByTimeout()
    {
        var session = NewFullSession();
        var start = DateTimeOffset.UtcNow;
        session.Start(TimeSpan.FromSeconds(2), TimeSpan.Zero, start);

        session.DeclareTimeoutIfExpired(start + TimeSpan.FromSeconds(1));
        Assert.False(session.Game!.IsGameOver);

        session.DeclareTimeoutIfExpired(start + TimeSpan.FromSeconds(3));

        Assert.True(session.Game.IsGameOver);
        Assert.Equal(GameEndReason.Timeout, session.Game.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, session.Game.EndResult.Value.Winner);
    }

    [Fact]
    public void DeclareTimeoutIfExpired_BeforeTheGameHasStarted_DoesNothing()
    {
        var session = NewFullSession();

        session.DeclareTimeoutIfExpired(DateTimeOffset.UtcNow);
    }
}
