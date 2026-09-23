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
    BotRunner botRunner,
    GameArchive archive,
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
        var session = registry.CreateRoom(kind, UserId, DefaultInitialClock, DefaultClockIncrement);
        await SwitchToRoomGroupAsync(session.Room.Code);
        return GameDtoMapper.ToRoomDto(session);
    }

    public Task<IReadOnlyList<GameHistoryEntryDto>> GetMyGameHistory(int limit = 50) =>
        history.ForUserAsync(UserId, Math.Clamp(limit, 1, 200));

    public Task<GameHistoryDetailDto?> GetGameHistoryEntry(Guid id) => history.ByIdAsync(id);

    public Task<GameHistoryDetailDto?> GetGameHistoryByRoomCode(string code) => history.ByRoomCodeAsync(code);

    public async Task<RoomStateDto?> JoinRoom(string code)
    {
        if (registry.Find(code) is not { } session)
            return null;

        await SwitchToRoomGroupAsync(session.Room.Code);
        return GameDtoMapper.ToRoomDto(session);
    }

    private async Task SwitchToRoomGroupAsync(string code)
    {
        if (Context.Items.TryGetValue(CurrentRoomGroupKey, out var previous) && previous is string previousCode && previousCode != code)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, previousCode);

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        Context.Items[CurrentRoomGroupKey] = code;
    }

    public async Task ClaimSeat(string code, SeatId seatId)
    {
        var session = registry.Get(code);
        session.Room.ClaimSeat(seatId, UserId, DisplayName);
        await BroadcastRoom(session);
    }

    public async Task LeaveSeat(string code, SeatId seatId)
    {
        var session = registry.Get(code);
        session.Room.LeaveSeat(seatId, UserId);
        await BroadcastRoom(session);
    }

    public async Task SetSeatBot(string code, SeatId seatId, BotDifficulty difficulty)
    {
        var session = registry.Get(code);
        session.Room.SetBot(seatId, difficulty);
        await BroadcastRoom(session);
    }

    public async Task ClearSeat(string code, SeatId seatId)
    {
        var session = registry.Get(code);
        session.Room.ClearSeat(seatId);
        await BroadcastRoom(session);
    }

    public async Task SetClockSettings(string code, int initialSeconds, int incrementSeconds)
    {
        var session = registry.Get(code);
        if (session.Room.HostUserId != UserId)
            throw new HubException("Only the room's host can change the clock settings.");

        session.Room.SetClockSettings(TimeSpan.FromSeconds(initialSeconds), TimeSpan.FromSeconds(incrementSeconds));
        await BroadcastRoom(session);
    }

    public async Task StartGame(string code)
    {
        var session = registry.Get(code);
        if (session.Room.HostUserId != UserId)
            throw new HubException("Only the room's host can start the game.");

        session.Start(session.Room.InitialClock, session.Room.ClockIncrement, DateTimeOffset.UtcNow);
        await BroadcastGame(session, "GameStarted");
        botRunner.ScheduleBotTurns(code);
    }

    public async Task PerformAction(string code, GameAction action)
    {
        var session = registry.Get(code);
        var seat = EnsureCallerMayAct(session);

        try
        {
            session.Apply(action, seat, DateTimeOffset.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            throw new HubException(ex.Message);
        }

        await BroadcastGame(session, "GameUpdated");
        botRunner.ScheduleBotTurns(code);
    }

    public Task<GameStateEnvelopeDto> GetGameState(string code)
    {
        var session = registry.Get(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        return Task.FromResult(session.ToStateDto());
    }

    public async Task Resign(string code)
    {
        var session = registry.Get(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        var seat = session.Room.FindSeatOf(UserId) ?? throw new HubException("You are not seated in this room.");
        session.Game.Resign(seat.Side);
        await BroadcastGame(session, "GameUpdated");
    }

    private Task BroadcastRoom(IRoomSession session) =>
        Clients.Group(session.Room.Code).SendAsync("RoomUpdated", GameDtoMapper.ToRoomDto(session));

    private async Task BroadcastGame(IRoomSession session, string eventName)
    {
        await Clients.Group(session.Room.Code).SendAsync(eventName, session.ToUpdateDto());
        await archive.RecordIfFinishedAsync(session);
    }

    private SeatId EnsureCallerMayAct(IRoomSession session)
    {
        if (session.Game is null)
            throw new HubException("Game has not started.");

        var seat = session.Room.FindSeatOf(UserId) ?? throw new HubException("You are not seated in this room.");
        if (!session.ActiveSeats.Contains(seat))
            throw new HubException("It's not your turn.");

        return seat;
    }
}
