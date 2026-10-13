using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace ChessLab.Web.Client.Services;

public sealed class GameClient : IAsyncDisposable
{
    public HubConnection Connection { get; }

    public event Action<RoomStateDto>? RoomUpdated;
    public event Action<GameUpdateEnvelopeDto>? GameUpdated;
    public event Func<Task>? Reconnected;
    public event Action<string>? NoticeReceived;

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
        Connection.On<string>("RoomNotice", message => NoticeReceived?.Invoke(message));
        Connection.Reconnected += _ => NotifyReconnectedAsync();
    }

    private async Task NotifyReconnectedAsync()
    {
        if (Reconnected is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<Func<Task>>())
            await handler();
    }

    public async Task EnsureConnectedAsync()
    {
        if (Connection.State == HubConnectionState.Disconnected)
            await Connection.StartAsync();
    }

    public Task<RoomStateDto> CreateRoomAsync(GameKind kind = GameKind.HandAndBrain) =>
        CallAsync<RoomStateDto>("CreateRoom", kind);

    public Task<RoomStateDto?> JoinRoomAsync(string code) => CallAsync<RoomStateDto?>("JoinRoom", code);

    public Task<IReadOnlyList<GameHistoryEntryDto>> GetMyGameHistoryAsync(int limit = 50) =>
        CallAsync<IReadOnlyList<GameHistoryEntryDto>>("GetMyGameHistory", limit);

    public Task<GameHistoryDetailDto?> GetGameHistoryEntryAsync(Guid id) =>
        CallAsync<GameHistoryDetailDto?>("GetGameHistoryEntry", id);

    public Task<GameHistoryDetailDto?> GetGameHistoryByRoomCodeAsync(string code) =>
        CallAsync<GameHistoryDetailDto?>("GetGameHistoryByRoomCode", code);

    public Task ClaimSeatAsync(string code, SeatId seatId) => CallAsync("ClaimSeat", code, seatId);

    public Task LeaveSeatAsync(string code, SeatId seatId) => CallAsync("LeaveSeat", code, seatId);

    public Task SetSeatBotAsync(string code, SeatId seatId, BotDifficulty difficulty) =>
        CallAsync("SetSeatBot", code, seatId, difficulty);

    public Task ClearSeatAsync(string code, SeatId seatId) => CallAsync("ClearSeat", code, seatId);

    public Task SetClockSettingsAsync(string code, int initialSeconds, int incrementSeconds) =>
        CallAsync("SetClockSettings", code, initialSeconds, incrementSeconds);

    public Task StartGameAsync(string code) => CallAsync("StartGame", code);

    public Task<GameStateEnvelopeDto> GetGameStateAsync(string code) =>
        CallAsync<GameStateEnvelopeDto>("GetGameState", code);

    public Task PerformActionAsync(string code, GameAction action) => CallAsync("PerformAction", code, action);

    public Task ResignAsync(string code) => CallAsync("Resign", code);

    private async Task CallAsync(string method, params object?[] arguments)
    {
        try
        {
            await Connection.InvokeCoreAsync(method, arguments);
        }
        catch (HubException rejection)
        {
            throw new HubException(HubRejection.MessageOf(rejection));
        }
    }

    private async Task<TResult> CallAsync<TResult>(string method, params object?[] arguments)
    {
        try
        {
            return await Connection.InvokeCoreAsync<TResult>(method, arguments);
        }
        catch (HubException rejection)
        {
            throw new HubException(HubRejection.MessageOf(rejection));
        }
    }

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
