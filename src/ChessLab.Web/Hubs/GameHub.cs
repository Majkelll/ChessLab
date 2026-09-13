using System.Security.Claims;
using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
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
            await SwitchToRoomGroupAsync(cardChessSession.Room.Code);
            return GameDtoMapper.ToRoomDto(cardChessSession);
        }

        if (kind == GameKind.ArcaneChess)
        {
            var arcaneChessSession = registry.CreateArcaneChessRoom(UserId, DefaultInitialClock, DefaultClockIncrement);
            await SwitchToRoomGroupAsync(arcaneChessSession.Room.Code);
            return GameDtoMapper.ToRoomDto(arcaneChessSession);
        }

        var session = registry.CreateRoom(UserId, DefaultInitialClock, DefaultClockIncrement);
        await SwitchToRoomGroupAsync(session.Room.Code);
        return GameDtoMapper.ToRoomDto(session);
    }

    public Task<IReadOnlyList<GameHistoryEntryDto>> GetMyGameHistory(int limit = 50) =>
        history.ForUserAsync(UserId, Math.Clamp(limit, 1, 200));

    public Task<GameHistoryDetailDto?> GetGameHistoryEntry(Guid id) => history.ByIdAsync(id);

    /// <summary>Lets a stale room link land on the finished game it used to be, now that the room
    /// itself is gone — the record outlives the in-memory room.</summary>
    public Task<GameHistoryDetailDto?> GetGameHistoryByRoomCode(string code) => history.ByRoomCodeAsync(code);

    public async Task<RoomStateDto?> JoinRoom(string code)
    {
        if (registry.FindAny(code) is not { } session)
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
        var session = registry.GetAny(code);
        session.Room.ClaimSeat(seatId, UserId, DisplayName);
        await BroadcastRoom(session);
    }

    public async Task LeaveSeat(string code, SeatId seatId)
    {
        var session = registry.GetAny(code);
        session.Room.LeaveSeat(seatId, UserId);
        await BroadcastRoom(session);
    }

    public async Task SetSeatBot(string code, SeatId seatId, BotDifficulty difficulty)
    {
        var session = registry.GetAny(code);
        session.Room.SetBot(seatId, difficulty);
        await BroadcastRoom(session);
    }

    public async Task ClearSeat(string code, SeatId seatId)
    {
        var session = registry.GetAny(code);
        session.Room.ClearSeat(seatId);
        await BroadcastRoom(session);
    }

    public async Task SetClockSettings(string code, int initialSeconds, int incrementSeconds)
    {
        var session = registry.GetAny(code);
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

    public async Task SelectPieceKind(string code, PieceKind kind)
    {
        var session = registry.Get(code);
        EnsureActiveSeatIsCaller(session);
        session.Game!.SelectPieceKind(kind);
        await BroadcastGame(session, "GameUpdated");
        botRunner.ScheduleBotTurns(code);
    }

    public async Task MakeMove(string code, Square from, Square to, PieceKind? promoteTo)
    {
        var session = registry.Get(code);
        EnsureActiveSeatIsCaller(session);
        session.MakeMove(from, to, promoteTo, DateTimeOffset.UtcNow);
        await BroadcastGame(session, "GameUpdated");
        botRunner.ScheduleBotTurns(code);
    }

    public Task<GameStateDto> GetGameState(string code)
    {
        var session = registry.Get(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        return Task.FromResult(GameDtoMapper.ToGameDto(session));
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

    public async Task StartCardChessGame(string code)
    {
        var session = registry.GetCardChess(code);
        if (session.Room.HostUserId != UserId)
            throw new HubException("Only the room's host can start the game.");

        session.Start(session.Room.InitialClock, session.Room.ClockIncrement, DateTimeOffset.UtcNow);
        await BroadcastCardChessGame(session, "CardChessGameStarted");
        botRunner.ScheduleCardChessBotTurns(code);
    }

    public async Task MakeCardChessMove(string code, Square from, Square to, PieceKind? promoteTo)
    {
        var session = registry.GetCardChess(code);
        EnsureCardChessActiveSeatIsCaller(session);
        session.MakeMove(from, to, promoteTo, DateTimeOffset.UtcNow);
        await BroadcastCardChessGame(session, "CardChessGameUpdated");
        botRunner.ScheduleCardChessBotTurns(code);
    }

    public async Task SelectCardChessReroll(string code, IReadOnlyList<CardRank> cards)
    {
        var session = registry.GetCardChess(code);
        EnsureCardChessActiveSeatIsCaller(session);
        session.SelectCardsForReroll(cards);
        await BroadcastCardChessGame(session, "CardChessGameUpdated");
    }

    public Task<CardChessStateDto> GetCardChessState(string code)
    {
        var session = registry.GetCardChess(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        return Task.FromResult(GameDtoMapper.ToCardChessDto(session));
    }

    public async Task ResignCardChess(string code)
    {
        var session = registry.GetCardChess(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        var seat = session.Room.FindSeatOf(UserId) ?? throw new HubException("You are not seated in this room.");
        session.Game.Resign(seat.Side);
        await BroadcastCardChessGame(session, "CardChessGameUpdated");
    }

    public async Task StartArcaneChessGame(string code)
    {
        var session = registry.GetArcaneChess(code);
        if (session.Room.HostUserId != UserId)
            throw new HubException("Only the room's host can start the game.");

        session.Start(session.Room.InitialClock, session.Room.ClockIncrement, DateTimeOffset.UtcNow);
        await BroadcastArcaneChessGame(session, "ArcaneChessGameStarted");
        botRunner.ScheduleArcaneChessBotTurns(code);
    }

    public async Task MakeArcaneChessMove(string code, Square from, Square to, PieceKind? promoteTo)
    {
        var session = registry.GetArcaneChess(code);
        EnsureArcaneChessActiveSeatIsCaller(session);
        session.MakeMove(from, to, promoteTo, DateTimeOffset.UtcNow);
        await BroadcastArcaneChessGame(session, "ArcaneChessGameUpdated");
        botRunner.ScheduleArcaneChessBotTurns(code);
    }

    public async Task CastArcaneSpell(string code, SpellRank spell, SpellTarget target)
    {
        var session = registry.GetArcaneChess(code);
        EnsureArcaneChessActiveSeatIsCaller(session);

        try
        {
            session.CastSpell(spell, target);
        }
        catch (InvalidOperationException ex)
        {
            // An illegal target is the player picking the wrong square, not a server fault — send
            // the rule that rejected it so the UI can show it next to the spell.
            throw new HubException(ex.Message);
        }

        await BroadcastArcaneChessGame(session, "ArcaneChessGameUpdated");
    }

    public async Task SelectArcaneChessReroll(string code, IReadOnlyList<CardRank> cards)
    {
        var session = registry.GetArcaneChess(code);
        EnsureArcaneChessActiveSeatIsCaller(session);
        session.SelectCardsForReroll(cards);
        await BroadcastArcaneChessGame(session, "ArcaneChessGameUpdated");
    }

    public Task<ArcaneChessStateDto> GetArcaneChessState(string code)
    {
        var session = registry.GetArcaneChess(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        return Task.FromResult(GameDtoMapper.ToArcaneChessDto(session));
    }

    public async Task ResignArcaneChess(string code)
    {
        var session = registry.GetArcaneChess(code);
        if (session.Game is null)
            throw new HubException("Game has not started.");

        var seat = session.Room.FindSeatOf(UserId) ?? throw new HubException("You are not seated in this room.");
        session.Game.Resign(seat.Side);
        await BroadcastArcaneChessGame(session, "ArcaneChessGameUpdated");
    }

    private Task BroadcastRoom(IRoomSession session) =>
        Clients.Group(session.Room.Code).SendAsync("RoomUpdated", GameDtoMapper.ToRoomDto(session));

    private async Task BroadcastGame(GameSession session, string eventName)
    {
        await Clients.Group(session.Room.Code).SendAsync(eventName, GameDtoMapper.ToGameUpdateDto(session));
        await archive.RecordIfFinishedAsync(session);
    }

    private async Task BroadcastCardChessGame(CardChessSession session, string eventName)
    {
        await Clients.Group(session.Room.Code).SendAsync(eventName, GameDtoMapper.ToCardChessUpdateDto(session));
        await archive.RecordIfFinishedAsync(session);
    }

    private async Task BroadcastArcaneChessGame(ArcaneChessSession session, string eventName)
    {
        await Clients.Group(session.Room.Code).SendAsync(eventName, GameDtoMapper.ToArcaneChessUpdateDto(session));
        await archive.RecordIfFinishedAsync(session);
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

    private void EnsureArcaneChessActiveSeatIsCaller(ArcaneChessSession session)
    {
        if (session.Game is null)
            throw new HubException("Game has not started.");

        var occupant = session.Room.Seats[session.ActiveSeat];
        if (occupant.Kind != OccupantKind.Human || occupant.UserId != UserId)
            throw new HubException("It's not your turn.");
    }
}
