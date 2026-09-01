using System.Security.Claims;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;
using ChessLab.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Hubs;

[Authorize]
public sealed class GameHub(RoomRegistry registry, BotRunner botRunner, IConfiguration configuration) : Hub
{
    // Seeds a new room's clock (10 min / no increment by default) — the host can then change it
    // per-room from the lobby via SetClockSettings, any time before the game starts. Also
    // configurable at the server level (not just for production tuning) so E2E tests can seed a
    // near-instant clock to exercise the timeout path without waiting on a real 10 minutes.
    private TimeSpan DefaultInitialClock =>
        TimeSpan.FromSeconds(configuration.GetValue("Game:InitialClockSeconds", 600));

    private TimeSpan DefaultClockIncrement =>
        TimeSpan.FromSeconds(configuration.GetValue("Game:ClockIncrementSeconds", 0));

    private Guid UserId => Guid.Parse(Context.User!.FindFirstValue("buid")!);
    private string DisplayName => Context.User!.Identity?.Name ?? "Gracz";

    public async Task<RoomStateDto> CreateRoom(GameKind kind = GameKind.HandAndBrain)
    {
        if (kind == GameKind.CardChess)
        {
            var cardChessSession = registry.CreateCardChessRoom(UserId, DefaultInitialClock, DefaultClockIncrement);
            await Groups.AddToGroupAsync(Context.ConnectionId, cardChessSession.Room.Code);
            return GameDtoMapper.ToRoomDto(cardChessSession);
        }

        var session = registry.CreateRoom(UserId, DefaultInitialClock, DefaultClockIncrement);
        await Groups.AddToGroupAsync(Context.ConnectionId, session.Room.Code);
        return GameDtoMapper.ToRoomDto(session);
    }

    public async Task<RoomStateDto> JoinRoom(string code)
    {
        var session = registry.GetAny(code);
        await Groups.AddToGroupAsync(Context.ConnectionId, session.Room.Code);
        return GameDtoMapper.ToRoomDto(session);
    }

    public async Task<RoomStateDto> ClaimSeat(string code, SeatId seatId)
    {
        var session = registry.GetAny(code);
        session.Room.ClaimSeat(seatId, UserId, DisplayName);
        return await BroadcastRoom(session);
    }

    public async Task<RoomStateDto> LeaveSeat(string code, SeatId seatId)
    {
        var session = registry.GetAny(code);
        session.Room.LeaveSeat(seatId, UserId);
        return await BroadcastRoom(session);
    }

    public async Task<RoomStateDto> SetSeatBot(string code, SeatId seatId, BotDifficulty difficulty)
    {
        var session = registry.GetAny(code);
        session.Room.SetBot(seatId, difficulty);
        return await BroadcastRoom(session);
    }

    public async Task<RoomStateDto> ClearSeat(string code, SeatId seatId)
    {
        var session = registry.GetAny(code);
        session.Room.ClearSeat(seatId);
        return await BroadcastRoom(session);
    }

    public async Task<RoomStateDto> SetClockSettings(string code, int initialSeconds, int incrementSeconds)
    {
        var session = registry.GetAny(code);
        if (session.Room.HostUserId != UserId)
            throw new HubException("Only the room's host can change the clock settings.");

        session.Room.SetClockSettings(TimeSpan.FromSeconds(initialSeconds), TimeSpan.FromSeconds(incrementSeconds));
        return await BroadcastRoom(session);
    }

    public async Task<GameStateDto> StartGame(string code)
    {
        var session = registry.Get(code);
        if (session.Room.HostUserId != UserId)
            throw new HubException("Only the room's host can start the game.");

        session.Start(session.Room.InitialClock, session.Room.ClockIncrement, DateTimeOffset.UtcNow);
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

    public async Task<CardChessStateDto> StartCardChessGame(string code)
    {
        var session = registry.GetCardChess(code);
        if (session.Room.HostUserId != UserId)
            throw new HubException("Only the room's host can start the game.");

        session.Start(session.Room.InitialClock, session.Room.ClockIncrement, DateTimeOffset.UtcNow);
        var dto = await BroadcastCardChessGame(session, "CardChessGameStarted");
        botRunner.ScheduleCardChessBotTurns(code);
        return dto;
    }

    public async Task<CardChessStateDto> MakeCardChessMove(string code, Square from, Square to, PieceKind? promoteTo)
    {
        var session = registry.GetCardChess(code);
        EnsureCardChessActiveSeatIsCaller(session);
        session.MakeMove(from, to, promoteTo, DateTimeOffset.UtcNow);
        var dto = await BroadcastCardChessGame(session, "CardChessGameUpdated");
        botRunner.ScheduleCardChessBotTurns(code);
        return dto;
    }

    public Task<CardChessStateDto> GetCardChessState(string code)
    {
        var session = registry.GetCardChess(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        return Task.FromResult(GameDtoMapper.ToCardChessDto(session));
    }

    public async Task<CardChessStateDto> ResignCardChess(string code)
    {
        var session = registry.GetCardChess(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        var seat = session.Room.FindSeatOf(UserId) ?? throw new HubException("You are not seated in this room.");
        session.Game.Resign(seat.Side);
        return await BroadcastCardChessGame(session, "CardChessGameUpdated");
    }

    private async Task<RoomStateDto> BroadcastRoom(IRoomSession session)
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

    private async Task<CardChessStateDto> BroadcastCardChessGame(CardChessSession session, string eventName)
    {
        var dto = GameDtoMapper.ToCardChessDto(session);
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

    private void EnsureCardChessActiveSeatIsCaller(CardChessSession session)
    {
        if (session.Game is null)
            throw new HubException("Game has not started.");

        var occupant = session.Room.Seats[session.ActiveSeat];
        if (occupant.Kind != OccupantKind.Human || occupant.UserId != UserId)
            throw new HubException("It's not your turn.");
    }
}
