using System.Collections.Concurrent;

namespace ChessLab.Web.Services;

public sealed class RoomConnections
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Guid>> usersByRoom = new();
    private readonly ConcurrentDictionary<string, string> roomOfConnection = new();

    public void Join(string code, string connectionId, Guid userId)
    {
        Leave(connectionId);
        usersByRoom.GetOrAdd(code, static _ => new ConcurrentDictionary<string, Guid>())[connectionId] = userId;
        roomOfConnection[connectionId] = code;
    }

    public void Leave(string connectionId)
    {
        if (roomOfConnection.TryRemove(connectionId, out var code) && usersByRoom.TryGetValue(code, out var users))
            users.TryRemove(connectionId, out _);
    }

    public IReadOnlyList<KeyValuePair<string, Guid>> In(string code) =>
        usersByRoom.TryGetValue(code, out var users) ? [.. users] : [];

    public void Forget(string code)
    {
        if (!usersByRoom.TryRemove(code, out var users))
            return;

        foreach (var connectionId in users.Keys)
            roomOfConnection.TryRemove(connectionId, out _);
    }
}
