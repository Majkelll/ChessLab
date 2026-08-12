using System.Security.Claims;
using BrainAndHand.Core.Chess;
using BrainAndHand.Core.Contracts;
using BrainAndHand.Core.Rooms;
using BrainAndHand.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BrainAndHand.Web.Hubs;

[Authorize]
public sealed class GameHub(RoomRegistry registry, BotRunner botRunner) : Hub
{
    private static readonly TimeSpan InitialClock = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ClockIncrement = TimeSpan.FromSeconds(5);

    private Guid UserId => Guid.Parse(Context.User!.FindFirstValue("buid")!);
    private string DisplayName => Context.User!.Identity?.Name ?? "Gracz";

    public async Task<RoomStateDto> CreateRoom()
    {
        var session = registry.CreateRoom(UserId);
        await Groups.AddToGroupAsync(Context.ConnectionId, session.Room.Code);
        return GameDtoMapper.ToRoomDto(session);
    }

    public async Task<RoomStateDto> JoinRoom(string code)
    {
        var session = registry.Get(code);
        await Groups.AddToGroupAsync(Context.ConnectionId, session.Room.Code);
        return GameDtoMapper.ToRoomDto(session);
    }

    public async Task<RoomStateDto> ClaimSeat(string code, SeatId seatId)
    {
        var session = registry.Get(code);
        session.Room.ClaimSeat(seatId, UserId, DisplayName);
        return await BroadcastRoom(session);
    }

    public async Task<RoomStateDto> LeaveSeat(string code, SeatId seatId)
    {
        var session = registry.Get(code);
        session.Room.LeaveSeat(seatId, UserId);
        return await BroadcastRoom(session);
    }

    public async Task<RoomStateDto> SetSeatBot(string code, SeatId seatId, BotDifficulty difficulty)
    {
        var session = registry.Get(code);
        session.Room.SetBot(seatId, difficulty);
        return await BroadcastRoom(session);
    }

    public async Task<RoomStateDto> ClearSeat(string code, SeatId seatId)
    {
        var session = registry.Get(code);
        session.Room.ClearSeat(seatId);
        return await BroadcastRoom(session);
    }

    public async Task<GameStateDto> StartGame(string code)
    {
        var session = registry.Get(code);
        if (session.Room.HostUserId != UserId)
            throw new HubException("Only the room's host can start the game.");

        session.Start(InitialClock, ClockIncrement, DateTimeOffset.UtcNow);
        var dto = await BroadcastGame(session, "GameStarted");
        botRunner.ScheduleBotTurns(code);
        return dto;
    }

    public async Task<GameStateDto> SelectPieceKind(string code, PieceKind kind)
    {
        var session = registry.Get(code);
        EnsureActiveSeatIsCaller(session);
        session.Game!.SelectPieceKind(kind);
        var dto = await BroadcastGame(session, "GameUpdated");
        botRunner.ScheduleBotTurns(code);
        return dto;
    }

    public async Task<GameStateDto> MakeMove(string code, Square from, Square to, PieceKind? promoteTo)
    {
        var session = registry.Get(code);
        EnsureActiveSeatIsCaller(session);
        session.MakeMove(from, to, promoteTo, DateTimeOffset.UtcNow);
        var dto = await BroadcastGame(session, "GameUpdated");
        botRunner.ScheduleBotTurns(code);
        return dto;
    }

    public Task<GameStateDto> GetGameState(string code)
    {
        var session = registry.Get(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        return Task.FromResult(GameDtoMapper.ToGameDto(session));
    }

    public async Task<GameStateDto> Resign(string code)
    {
        var session = registry.Get(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        var seat = session.Room.FindSeatOf(UserId) ?? throw new HubException("You are not seated in this room.");
        session.Game.Resign(seat.Side);
        return await BroadcastGame(session, "GameUpdated");
    }

    private async Task<RoomStateDto> BroadcastRoom(GameSession session)
    {
        var dto = GameDtoMapper.ToRoomDto(session);
        await Clients.Group(session.Room.Code).SendAsync("RoomUpdated", dto);
        return dto;
    }

    private async Task<GameStateDto> BroadcastGame(GameSession session, string eventName)
    {
        var dto = GameDtoMapper.ToGameDto(session);
        await Clients.Group(session.Room.Code).SendAsync(eventName, dto);
        return dto;
    }

    private void EnsureActiveSeatIsCaller(GameSession session)
    {
        if (session.Game is null)
            throw new HubException("Game has not started.");

        var occupant = session.Room.Seats[session.ActiveSeat];
        if (occupant.Kind != OccupantKind.Human || occupant.UserId != UserId)
            throw new HubException("It's not your turn.");
    }
}
