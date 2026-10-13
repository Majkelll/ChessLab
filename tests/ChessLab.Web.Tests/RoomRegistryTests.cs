using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;
using ChessLab.Web.Services;

namespace ChessLab.Web.Tests;

public class RoomRegistryTests
{
    private static readonly SeatId White = new(Side.White, SeatRole.Player);
    private static readonly SeatId Black = new(Side.Black, SeatRole.Player);

    private static IRoomSession FinishedRoom(RoomRegistry registry)
    {
        var session = registry.CreateRoom(GameKind.AliceChess, Guid.NewGuid());
        session.Room.SetBot(White, BotDifficulty.Easy);
        session.Room.SetBot(Black, BotDifficulty.Easy);
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, DateTimeOffset.UtcNow);
        session.Game!.Resign(Side.White);
        return session;
    }

    [Fact]
    public void RemoveStale_KeepsAFreshLobbyAndDropsOneLeftUnstartedForTwoHours()
    {
        var registry = new RoomRegistry();
        var code = registry.CreateRoom(GameKind.CardChess, Guid.NewGuid()).Room.Code;

        Assert.Empty(registry.RemoveStale(DateTimeOffset.UtcNow.AddMinutes(119), _ => false));
        Assert.Equal([code], registry.RemoveStale(DateTimeOffset.UtcNow.AddMinutes(121), _ => false));
        Assert.Null(registry.Find(code));
    }

    [Fact]
    public void RemoveStale_DropsAnArchivedFinishedRoomFifteenMinutesAfterItWasSeenFinished()
    {
        var registry = new RoomRegistry();
        var code = FinishedRoom(registry).Room.Code;
        var seenFinishedAt = DateTimeOffset.UtcNow;

        Assert.Empty(registry.RemoveStale(seenFinishedAt, _ => true));
        Assert.Empty(registry.RemoveStale(seenFinishedAt.AddMinutes(14), _ => true));
        Assert.Equal([code], registry.RemoveStale(seenFinishedAt.AddMinutes(15), _ => true));
    }

    [Fact]
    public void RemoveStale_KeepsAFinishedRoomWhoseGameIsNotArchivedYet()
    {
        var registry = new RoomRegistry();
        FinishedRoom(registry);
        var seenFinishedAt = DateTimeOffset.UtcNow;
        registry.RemoveStale(seenFinishedAt, _ => false);

        Assert.Empty(registry.RemoveStale(seenFinishedAt.AddHours(23), _ => false));
        Assert.Single(registry.RemoveStale(seenFinishedAt.AddHours(24), _ => false));
    }

    [Fact]
    public void RemoveStale_NeverDropsARoomWhoseGameIsRunning()
    {
        var registry = new RoomRegistry();
        var session = registry.CreateRoom(GameKind.AliceChess, Guid.NewGuid());
        session.Room.SetBot(White, BotDifficulty.Easy);
        session.Room.SetBot(Black, BotDifficulty.Easy);
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, DateTimeOffset.UtcNow);

        Assert.Empty(registry.RemoveStale(DateTimeOffset.UtcNow.AddDays(30), _ => true));
    }

    [Fact]
    public void OpenRoomsHostedBy_CountsOnlyRoomsWhoseGameHasNotEnded()
    {
        var registry = new RoomRegistry();
        var host = Guid.NewGuid();
        registry.CreateRoom(GameKind.CardChess, host);
        registry.CreateRoom(GameKind.CardChess, Guid.NewGuid());
        FinishedRoom(registry);

        Assert.Equal(1, registry.OpenRoomsHostedBy(host));
    }
}

public class RoomConnectionsTests
{
    [Fact]
    public void Join_MovesAConnectionOutOfItsPreviousRoom()
    {
        var connections = new RoomConnections();
        var user = Guid.NewGuid();

        connections.Join("AAAAAA", "c1", user);
        connections.Join("BBBBBB", "c1", user);

        Assert.Empty(connections.In("AAAAAA"));
        Assert.Equal(user, Assert.Single(connections.In("BBBBBB")).Value);
    }

    [Fact]
    public void Leave_And_Forget_DropTheirConnections()
    {
        var connections = new RoomConnections();
        connections.Join("AAAAAA", "c1", Guid.NewGuid());
        connections.Join("AAAAAA", "c2", Guid.NewGuid());

        connections.Leave("c1");
        Assert.Single(connections.In("AAAAAA"));

        connections.Forget("AAAAAA");
        Assert.Empty(connections.In("AAAAAA"));
    }
}
