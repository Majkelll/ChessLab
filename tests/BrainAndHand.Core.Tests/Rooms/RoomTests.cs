using BrainAndHand.Core.Chess;
using BrainAndHand.Core.Rooms;

namespace BrainAndHand.Core.Tests.Rooms;

public class RoomTests
{
    private static readonly SeatId WhiteBrain = new(Side.White, SeatRole.Brain);
    private static readonly SeatId WhiteHand = new(Side.White, SeatRole.Hand);
    private static readonly SeatId BlackBrain = new(Side.Black, SeatRole.Brain);
    private static readonly SeatId BlackHand = new(Side.Black, SeatRole.Hand);

    [Fact]
    public void NewRoom_AllSeatsEmpty()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());

        Assert.All(room.Seats.Values, o => Assert.Equal(OccupantKind.Empty, o.Kind));
        Assert.False(room.IsFull);
    }

    [Fact]
    public void ClaimSeat_SetsHumanOccupant()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        var userId = Guid.NewGuid();

        room.ClaimSeat(WhiteBrain, userId, "Ala");

        Assert.Equal(OccupantKind.Human, room.Seats[WhiteBrain].Kind);
        Assert.Equal(userId, room.Seats[WhiteBrain].UserId);
        Assert.Equal(WhiteBrain, room.FindSeatOf(userId));
    }

    [Fact]
    public void ClaimSeat_AlreadyTaken_Throws()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        room.ClaimSeat(WhiteBrain, Guid.NewGuid(), "Ala");

        Assert.Throws<InvalidOperationException>(() => room.ClaimSeat(WhiteBrain, Guid.NewGuid(), "Bob"));
    }

    [Fact]
    public void ClaimSeat_MovesPlayerFromPreviousSeat()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        var userId = Guid.NewGuid();
        room.ClaimSeat(WhiteBrain, userId, "Ala");

        room.ClaimSeat(BlackHand, userId, "Ala");

        Assert.Equal(OccupantKind.Empty, room.Seats[WhiteBrain].Kind);
        Assert.Equal(OccupantKind.Human, room.Seats[BlackHand].Kind);
        Assert.Equal(BlackHand, room.FindSeatOf(userId));
    }

    [Fact]
    public void LeaveSeat_ClearsOwnSeatOnly()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        var userId = Guid.NewGuid();
        room.ClaimSeat(WhiteBrain, userId, "Ala");

        room.LeaveSeat(WhiteBrain, Guid.NewGuid());
        Assert.Equal(OccupantKind.Human, room.Seats[WhiteBrain].Kind);

        room.LeaveSeat(WhiteBrain, userId);
        Assert.Equal(OccupantKind.Empty, room.Seats[WhiteBrain].Kind);
    }

    [Fact]
    public void SetBot_OnHumanSeat_Throws()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        room.ClaimSeat(WhiteBrain, Guid.NewGuid(), "Ala");

        Assert.Throws<InvalidOperationException>(() => room.SetBot(WhiteBrain, BotDifficulty.Easy));
    }

    [Fact]
    public void IsFull_TrueWhenAllSeatsResolved()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        room.ClaimSeat(WhiteBrain, Guid.NewGuid(), "Ala");
        room.ClaimSeat(WhiteHand, Guid.NewGuid(), "Bob");
        room.SetBot(BlackBrain, BotDifficulty.Easy);
        room.SetBot(BlackHand, BotDifficulty.Hard);

        Assert.True(room.IsFull);
    }

    [Fact]
    public void NewRoom_DefaultsToTenMinutesWithNoIncrement()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());

        Assert.Equal(TimeSpan.FromMinutes(10), room.InitialClock);
        Assert.Equal(TimeSpan.Zero, room.ClockIncrement);
    }

    [Fact]
    public void SetClockSettings_ChangesInitialClockAndIncrement()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());

        room.SetClockSettings(TimeSpan.FromMinutes(3), TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromMinutes(3), room.InitialClock);
        Assert.Equal(TimeSpan.FromSeconds(2), room.ClockIncrement);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SetClockSettings_NonPositiveInitial_Throws(int seconds)
    {
        var room = new Room("ABCDEF", Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            room.SetClockSettings(TimeSpan.FromSeconds(seconds), TimeSpan.Zero));
    }

    [Fact]
    public void SetClockSettings_NegativeIncrement_Throws()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            room.SetClockSettings(TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void SetClockSettings_AfterGameStarted_Throws()
    {
        var room = new Room("ABCDEF", Guid.NewGuid());
        room.ClaimSeat(WhiteBrain, Guid.NewGuid(), "Ala");
        room.ClaimSeat(WhiteHand, Guid.NewGuid(), "Bob");
        room.SetBot(BlackBrain, BotDifficulty.Easy);
        room.SetBot(BlackHand, BotDifficulty.Hard);
        var session = new GameSession(room);
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            room.SetClockSettings(TimeSpan.FromMinutes(5), TimeSpan.Zero));
    }
}
