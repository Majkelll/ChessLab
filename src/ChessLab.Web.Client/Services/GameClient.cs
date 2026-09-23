using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChessLab.Web.Client.Services;

/// <summary>Thin wrapper around the SignalR connection to GameHub, shared by the Lobby and Game pages.</summary>
public sealed class GameClient : IAsyncDisposable
{
    public HubConnection Connection { get; }

    public event Action<RoomStateDto>? RoomUpdated;
    public event Action<GameUpdateEnvelopeDto>? GameUpdated;

    public GameClient(NavigationManager navigationManager)
    {
        Connection = new HubConnectionBuilder()
            .WithUrl(navigationManager.ToAbsoluteUri("/hubs/game"))
            .WithAutomaticReconnect()
            .AddMessagePackProtocol()
            .Build();

        Connection.On<RoomStateDto>("RoomUpdated", dto => RoomUpdated?.Invoke(dto));
        Connection.On<GameUpdateEnvelopeDto>("GameStarted", dto => GameUpdated?.Invoke(dto));
        Connection.On<GameUpdateEnvelopeDto>("GameUpdated", dto => GameUpdated?.Invoke(dto));
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

    public Task<GameStateEnvelopeDto> GetGameStateAsync(string code) =>
        Connection.InvokeAsync<GameStateEnvelopeDto>("GetGameState", code);

    public Task PerformActionAsync(string code, GameAction action) =>
        Connection.InvokeAsync("PerformAction", code, action);

    public Task ResignAsync(string code) => Connection.InvokeAsync("Resign", code);

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
