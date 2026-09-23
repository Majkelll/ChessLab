using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Core.Tests.Rooms;

public class CardChessSessionTests
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);
    private static readonly SeatId Black = new(Side.Black, SeatRole.Player);

    private static CardChessSession NewFullSession()
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), GameKind.CardChess);
        room.ClaimSeat(White, Guid.NewGuid(), "Ala");
        room.ClaimSeat(Black, Guid.NewGuid(), "Bob");
        return new CardChessSession(room);
    }

    [Fact]
    public void Start_WhenRoomNotFull_Throws()
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), GameKind.CardChess);
        var session = new CardChessSession(room);

        Assert.Throws<InvalidOperationException>(() => session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Start_LocksRoomAndCreatesGame()
    {
        var session = NewFullSession();

        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, DateTimeOffset.UtcNow, new Random(1));

        Assert.True(session.Room.IsLocked);
        Assert.NotNull(session.Game);
    }

    [Fact]
    public void ActiveSeat_BeforeStart_Throws()
    {
        var session = NewFullSession();

        Assert.Throws<InvalidOperationException>(() => session.ActiveSeats);
    }

    [Fact]
    public void ActiveSeat_AtStart_IsWhitePlayer()
    {
        var session = NewFullSession();
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, DateTimeOffset.UtcNow, new Random(1));

        Assert.Equal(White, Assert.Single(session.ActiveSeats));
    }

    [Fact]
    public void MakeMove_DeductsElapsedWallClockTimeAndSwitchesTheActiveSeat()
    {
        var session = NewFullSession();
        var start = DateTimeOffset.UtcNow;
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(5), start, new Random(1));
        var move = session.Game!.AvailableMoves[0];

        session.MakeMove(move.From, move.To, move.PromoteTo, start + TimeSpan.FromSeconds(12));

        Assert.Equal(
            TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(12) + TimeSpan.FromSeconds(5),
            session.Game.Clock.WhiteRemaining);
        Assert.Equal(Black, Assert.Single(session.ActiveSeats));
    }

    [Fact]
    public void DeclareTimeoutIfExpired_WhenNoOneEverMoves_StillEndsTheGameByTimeout()
    {
        var session = NewFullSession();
        var start = DateTimeOffset.UtcNow;
        session.Start(TimeSpan.FromSeconds(2), TimeSpan.Zero, start, new Random(1));

        session.DeclareTimeoutIfExpired(start + TimeSpan.FromSeconds(1));
        Assert.False(session.Game!.IsGameOver);

        session.DeclareTimeoutIfExpired(start + TimeSpan.FromSeconds(3));

        Assert.True(session.Game.IsGameOver);
        Assert.Equal(GameEndReason.Timeout, session.Game.EndResult!.Value.Reason);
        Assert.Equal(Side.Black, session.Game.EndResult.Value.Winner);
    }
}
