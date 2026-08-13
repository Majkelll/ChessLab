using BrainAndHand.Core.Chess;
using BrainAndHand.Core.Contracts;
using BrainAndHand.Core.Rooms;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace BrainAndHand.Web.Client.Services;

/// <summary>Thin wrapper around the SignalR connection to GameHub, shared by the Lobby and Game pages.</summary>
public sealed class GameClient : IAsyncDisposable
{
    public HubConnection Connection { get; }

    public event Action<RoomStateDto>? RoomUpdated;
    public event Action<GameStateDto>? GameUpdated;

    public GameClient(NavigationManager navigationManager)
    {
        Connection = new HubConnectionBuilder()
            .WithUrl(navigationManager.ToAbsoluteUri("/hubs/game"))
            .WithAutomaticReconnect()
            .Build();

        Connection.On<RoomStateDto>("RoomUpdated", dto => RoomUpdated?.Invoke(dto));
        Connection.On<GameStateDto>("GameStarted", dto => GameUpdated?.Invoke(dto));
        Connection.On<GameStateDto>("GameUpdated", dto => GameUpdated?.Invoke(dto));
    }

    public async Task EnsureConnectedAsync()
    {
        if (Connection.State == HubConnectionState.Disconnected)
            await Connection.StartAsync();
    }

    public Task<RoomStateDto> CreateRoomAsync() => Connection.InvokeAsync<RoomStateDto>("CreateRoom");

    public Task<RoomStateDto> JoinRoomAsync(string code) => Connection.InvokeAsync<RoomStateDto>("JoinRoom", code);

    public Task<RoomStateDto> ClaimSeatAsync(string code, SeatId seatId) =>
        Connection.InvokeAsync<RoomStateDto>("ClaimSeat", code, seatId);

    public Task<RoomStateDto> LeaveSeatAsync(string code, SeatId seatId) =>
        Connection.InvokeAsync<RoomStateDto>("LeaveSeat", code, seatId);

    public Task<RoomStateDto> SetSeatBotAsync(string code, SeatId seatId, BotDifficulty difficulty) =>
        Connection.InvokeAsync<RoomStateDto>("SetSeatBot", code, seatId, difficulty);

    public Task<RoomStateDto> ClearSeatAsync(string code, SeatId seatId) =>
        Connection.InvokeAsync<RoomStateDto>("ClearSeat", code, seatId);

    public Task<RoomStateDto> SetClockSettingsAsync(string code, int initialSeconds, int incrementSeconds) =>
        Connection.InvokeAsync<RoomStateDto>("SetClockSettings", code, initialSeconds, incrementSeconds);

    public Task<GameStateDto> StartGameAsync(string code) => Connection.InvokeAsync<GameStateDto>("StartGame", code);

    public Task<GameStateDto> GetGameStateAsync(string code) => Connection.InvokeAsync<GameStateDto>("GetGameState", code);

    public Task<GameStateDto> SelectPieceKindAsync(string code, PieceKind kind) =>
        Connection.InvokeAsync<GameStateDto>("SelectPieceKind", code, kind);

    public Task<GameStateDto> MakeMoveAsync(string code, Square from, Square to, PieceKind? promoteTo) =>
        Connection.InvokeAsync<GameStateDto>("MakeMove", code, from, to, promoteTo);

    public Task<GameStateDto> ResignAsync(string code) => Connection.InvokeAsync<GameStateDto>("Resign", code);

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
