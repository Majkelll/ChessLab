using System.Security.Claims;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using ChessLab.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Hubs;

[Authorize]
public sealed class GameHub(
    RoomRegistry registry,
    RoomConnections connections,
    GameBroadcaster broadcaster,
    BotRunner botRunner,
    GameHistoryService history,
    IConfiguration configuration) : Hub
{
    private const string CurrentRoomGroupKey = "CurrentRoomGroup";

    private TimeSpan DefaultInitialClock =>
        TimeSpan.FromSeconds(configuration.GetValue("Game:InitialClockSeconds", 600));

    private TimeSpan DefaultClockIncrement =>
        TimeSpan.FromSeconds(configuration.GetValue("Game:ClockIncrementSeconds", 0));

    private Guid UserId => Guid.Parse(Context.User!.FindFirstValue("buid")!);
    private string DisplayName => Context.User!.Identity?.Name ?? "Gracz";

    public async Task<RoomStateDto> CreateRoom(GameKind kind = GameKind.HandAndBrain)
    {
        var userId = UserId;
        if (registry.OpenRoomsHostedBy(userId) >= RoomRegistry.OpenRoomsPerHost)
            throw new HubException(
                $"You already host {RoomRegistry.OpenRoomsPerHost} open rooms. Wait for one to finish or expire.");

        var session = await CreateRoomWithUnusedCodeAsync(kind, userId);
        await SwitchToRoomGroupAsync(session.Room.Code);
        return RoomDtoOf(session);
    }

    private async Task<IRoomSession> CreateRoomWithUnusedCodeAsync(GameKind kind, Guid userId)
    {
        while (true)
        {
            var session = registry.CreateRoom(kind, userId, DefaultInitialClock, DefaultClockIncrement);
            if (!await history.HasRecordForRoomCodeAsync(session.Room.Code))
                return session;

            registry.Remove(session.Room.Code);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Leave(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public Task<IReadOnlyList<GameHistoryEntryDto>> GetMyGameHistory(int limit = 50) =>
        history.ForUserAsync(UserId, Math.Clamp(limit, 1, 200));

    public Task<GameHistoryDetailDto?> GetGameHistoryEntry(Guid id) => history.ByIdAsync(id);

    public Task<GameHistoryDetailDto?> GetGameHistoryByRoomCode(string code) =>
        history.ByRoomCodeAsync(code, UserId);

    public async Task<RoomStateDto?> JoinRoom(string code)
    {
        if (registry.Find(code) is not { } session)
            return null;

        await SwitchToRoomGroupAsync(session.Room.Code);
        return RoomDtoOf(session);
    }

    private async Task SwitchToRoomGroupAsync(string code)
    {
        if (Context.Items.TryGetValue(CurrentRoomGroupKey, out var previous) && previous is string previousCode && previousCode != code)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previousCode);

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        Context.Items[CurrentRoomGroupKey] = code;
        connections.Join(code, Context.ConnectionId, UserId);
    }

    public Task ClaimSeat(string code, SeatId seatId)
    {
        var session = registry.Get(code);
        var userId = UserId;
        var displayName = DisplayName;
        return ChangeRoom(session, room => room.ClaimSeat(seatId, userId, displayName));
    }

    public Task LeaveSeat(string code, SeatId seatId)
    {
        var session = registry.Get(code);
        var userId = UserId;
        return ChangeRoom(session, room => room.LeaveSeat(seatId, userId));
    }

    public Task SetSeatBot(string code, SeatId seatId, BotDifficulty difficulty)
    {
        var session = registry.Get(code);
        EnsureCallerIsHost(session, "Only the room's host can add or change a bot.");
        return ChangeRoom(session, room => room.SetBot(seatId, difficulty));
    }

    public Task ClearSeat(string code, SeatId seatId)
    {
        var session = registry.Get(code);
        EnsureCallerIsHost(session, "Only the room's host can clear a seat.");
        return ChangeRoom(session, room => room.ClearSeat(seatId));
    }

    public Task SetClockSettings(string code, int initialSeconds, int incrementSeconds)
    {
        var session = registry.Get(code);
        EnsureCallerIsHost(session, "Only the room's host can change the clock settings.");
        return ChangeRoom(session, room =>
            room.SetClockSettings(TimeSpan.FromSeconds(initialSeconds), TimeSpan.FromSeconds(incrementSeconds)));
    }

    public async Task StartGame(string code)
    {
        var session = registry.Get(code);
        EnsureCallerIsHost(session, "Only the room's host can start the game.");

        lock (session)
            session.Start(session.Room.InitialClock, session.Room.ClockIncrement, DateTimeOffset.UtcNow);

        await BroadcastGame(session, GameBroadcaster.GameStarted);
    }

    public async Task PerformAction(string code, GameAction action)
    {
        var session = registry.Get(code);
        var userId = UserId;
        var now = DateTimeOffset.UtcNow;

        lock (session)
        {
            if (session.Game is null)
                throw new HubException("Game has not started.");
            if (session.Game.IsGameOver)
                throw new HubException("The game has already ended.");

            session.DeclareTimeoutIfExpired(now);
            if (session.HasActiveGame)
                session.Apply(action, SeatAllowedToAct(session, userId), now);
        }

        await BroadcastGame(session, "GameUpdated");
    }

    public Task<GameStateEnvelopeDto> GetGameState(string code)
    {
        var session = registry.Get(code);
        var userId = UserId;

        lock (session)
        {
            if (session.Game is null)
                throw new HubException("Game has not started.");

            return Task.FromResult(session.ToStateDto(session.Room.FindSeatOf(userId)?.Side));
        }
    }

    public async Task Resign(string code)
    {
        var session = registry.Get(code);
        var userId = UserId;

        lock (session)
        {
            if (session.Game is null)
                throw new HubException("Game has not started.");

            var seat = session.Room.FindSeatOf(userId) ?? throw new HubException("You are not seated in this room.");
            session.Game.Resign(seat.Side);
        }

        await BroadcastGame(session, "GameUpdated");
    }

    private static RoomStateDto RoomDtoOf(IRoomSession session)
    {
        lock (session)
            return GameDtoMapper.ToRoomDto(session);
    }

    private Task ChangeRoom(IRoomSession session, Action<Room> change)
    {
        RoomStateDto state;
        lock (session)
        {
            change(session.Room);
            state = GameDtoMapper.ToRoomDto(session);
        }

        return Clients.Group(session.Room.Code).SendAsync("RoomUpdated", state);
    }

    private async Task BroadcastGame(IRoomSession session, string eventName)
    {
        await broadcaster.GameChangedAsync(session, eventName);
        botRunner.ScheduleBotTurns(session.Room.Code);
    }

    private void EnsureCallerIsHost(IRoomSession session, string rejection)
    {
        if (session.Room.HostUserId != UserId)
            throw new HubException(rejection);
    }

    private static SeatId SeatAllowedToAct(IRoomSession session, Guid userId)
    {
        var seat = session.Room.FindSeatOf(userId) ?? throw new HubException("You are not seated in this room.");
        if (!session.SeatsAllowedToAct.Contains(seat))
            throw new HubException("It's not your turn.");

        return seat;
    }
}
