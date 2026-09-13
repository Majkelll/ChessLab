using ChessLab.Core.ArcaneChess;
using ChessLab.Core.CardChess;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChessLab.Web.Client.Services;

/// <summary>Thin wrapper around the SignalR connection to GameHub, shared by the Lobby and Game pages.</summary>
public sealed class GameClient : IAsyncDisposable
{
    public HubConnection Connection { get; }

    public event Action<RoomStateDto>? RoomUpdated;
    public event Action<GameUpdateDto>? GameUpdated;
    public event Action<CardChessUpdateDto>? CardChessGameUpdated;
    public event Action<ArcaneChessUpdateDto>? ArcaneChessGameUpdated;

    public GameClient(NavigationManager navigationManager)
    {
        Connection = new HubConnectionBuilder()
            .WithUrl(navigationManager.ToAbsoluteUri("/hubs/game"))
            .WithAutomaticReconnect()
            .AddMessagePackProtocol()
            .Build();

        Connection.On<RoomStateDto>("RoomUpdated", dto => RoomUpdated?.Invoke(dto));
        Connection.On<GameUpdateDto>("GameStarted", dto => GameUpdated?.Invoke(dto));
        Connection.On<GameUpdateDto>("GameUpdated", dto => GameUpdated?.Invoke(dto));
        Connection.On<CardChessUpdateDto>("CardChessGameStarted", dto => CardChessGameUpdated?.Invoke(dto));
        Connection.On<CardChessUpdateDto>("CardChessGameUpdated", dto => CardChessGameUpdated?.Invoke(dto));
        Connection.On<ArcaneChessUpdateDto>("ArcaneChessGameStarted", dto => ArcaneChessGameUpdated?.Invoke(dto));
        Connection.On<ArcaneChessUpdateDto>("ArcaneChessGameUpdated", dto => ArcaneChessGameUpdated?.Invoke(dto));
    }

    public async Task EnsureConnectedAsync()
    {
        if (Connection.State == HubConnectionState.Disconnected)
            await Connection.StartAsync();
    }

    public Task<RoomStateDto> CreateRoomAsync(GameKind kind = GameKind.HandAndBrain) =>
        Connection.InvokeAsync<RoomStateDto>("CreateRoom", kind);

    public Task<RoomStateDto?> JoinRoomAsync(string code) => Connection.InvokeAsync<RoomStateDto?>("JoinRoom", code);

    public Task<IReadOnlyList<GameHistoryEntryDto>> GetMyGameHistoryAsync(int limit = 50) =>
        Connection.InvokeAsync<IReadOnlyList<GameHistoryEntryDto>>("GetMyGameHistory", limit);

    public Task<GameHistoryDetailDto?> GetGameHistoryEntryAsync(Guid id) =>
        Connection.InvokeAsync<GameHistoryDetailDto?>("GetGameHistoryEntry", id);

    public Task<GameHistoryDetailDto?> GetGameHistoryByRoomCodeAsync(string code) =>
        Connection.InvokeAsync<GameHistoryDetailDto?>("GetGameHistoryByRoomCode", code);

    public Task ClaimSeatAsync(string code, SeatId seatId) => Connection.InvokeAsync("ClaimSeat", code, seatId);

    public Task LeaveSeatAsync(string code, SeatId seatId) => Connection.InvokeAsync("LeaveSeat", code, seatId);

    public Task SetSeatBotAsync(string code, SeatId seatId, BotDifficulty difficulty) =>
        Connection.InvokeAsync("SetSeatBot", code, seatId, difficulty);

    public Task ClearSeatAsync(string code, SeatId seatId) => Connection.InvokeAsync("ClearSeat", code, seatId);

    public Task SetClockSettingsAsync(string code, int initialSeconds, int incrementSeconds) =>
        Connection.InvokeAsync("SetClockSettings", code, initialSeconds, incrementSeconds);

    public Task StartGameAsync(string code) => Connection.InvokeAsync("StartGame", code);

    public Task<GameStateDto> GetGameStateAsync(string code) => Connection.InvokeAsync<GameStateDto>("GetGameState", code);

    public Task SelectPieceKindAsync(string code, PieceKind kind) => Connection.InvokeAsync("SelectPieceKind", code, kind);

    public Task MakeMoveAsync(string code, Square from, Square to, PieceKind? promoteTo) =>
        Connection.InvokeAsync("MakeMove", code, from, to, promoteTo);

    public Task ResignAsync(string code) => Connection.InvokeAsync("Resign", code);

    public Task StartCardChessGameAsync(string code) => Connection.InvokeAsync("StartCardChessGame", code);

    public Task MakeCardChessMoveAsync(string code, Square from, Square to, PieceKind? promoteTo) =>
        Connection.InvokeAsync("MakeCardChessMove", code, from, to, promoteTo);

    public Task<CardChessStateDto> GetCardChessStateAsync(string code) =>
        Connection.InvokeAsync<CardChessStateDto>("GetCardChessState", code);

    public Task ResignCardChessAsync(string code) => Connection.InvokeAsync("ResignCardChess", code);

    public Task SelectCardChessRerollAsync(string code, IReadOnlyList<CardRank> cards) =>
        Connection.InvokeAsync("SelectCardChessReroll", code, cards);

    public Task StartArcaneChessGameAsync(string code) => Connection.InvokeAsync("StartArcaneChessGame", code);

    public Task MakeArcaneChessMoveAsync(string code, Square from, Square to, PieceKind? promoteTo) =>
        Connection.InvokeAsync("MakeArcaneChessMove", code, from, to, promoteTo);

    public Task CastArcaneSpellAsync(string code, SpellRank spell, SpellTarget target) =>
        Connection.InvokeAsync("CastArcaneSpell", code, spell, target);

    public Task<ArcaneChessStateDto> GetArcaneChessStateAsync(string code) =>
        Connection.InvokeAsync<ArcaneChessStateDto>("GetArcaneChessState", code);

    public Task ResignArcaneChessAsync(string code) => Connection.InvokeAsync("ResignArcaneChess", code);

    public Task SelectArcaneChessRerollAsync(string code, IReadOnlyList<CardRank> cards) =>
        Connection.InvokeAsync("SelectArcaneChessReroll", code, cards);

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
