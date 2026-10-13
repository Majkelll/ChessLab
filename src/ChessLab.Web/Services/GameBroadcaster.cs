using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;
using ChessLab.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Services;

public sealed class GameBroadcaster(IHubContext<GameHub> hub, RoomConnections connections, GameArchive archive)
{
    public const string GameStarted = "GameStarted";

    public async Task GameChangedAsync(IRoomSession session, string eventName, CancellationToken ct = default)
    {
        var deliveries = DeliveriesOf(session);

        await Task.WhenAll(deliveries.Select(delivery =>
            hub.Clients.Client(delivery.ConnectionId).SendAsync(eventName, delivery.Update, ct)));

        if (eventName == GameStarted || !session.HasActiveGame)
            await hub.Clients.Group(session.Room.Code).SendAsync("RoomUpdated", RoomStateOf(session), ct);

        await archive.RecordIfFinishedAsync(session);
    }

    private static RoomStateDto RoomStateOf(IRoomSession session)
    {
        lock (session)
            return GameDtoMapper.ToRoomDto(session);
    }

    public Task NoticeAsync(string roomCode, string message) =>
        hub.Clients.Group(roomCode).SendAsync("RoomNotice", message);

    private IReadOnlyList<Delivery> DeliveriesOf(IRoomSession session)
    {
        var viewers = connections.In(session.Room.Code);

        lock (session)
        {
            var updates = new Dictionary<Viewer, GameUpdateEnvelopeDto>();
            var deliveries = new List<Delivery>(viewers.Count);

            foreach (var (connectionId, userId) in viewers)
            {
                var viewer = new Viewer(session.Room.FindSeatOf(userId)?.Side);
                if (!updates.TryGetValue(viewer, out var update))
                    updates[viewer] = update = session.ToUpdateDto(viewer.Side);

                deliveries.Add(new Delivery(connectionId, update));
            }

            return deliveries;
        }
    }

    private readonly record struct Viewer(Side? Side);

    private readonly record struct Delivery(string ConnectionId, GameUpdateEnvelopeDto Update);
}
