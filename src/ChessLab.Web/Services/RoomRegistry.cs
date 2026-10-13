using System.Collections.Concurrent;
using ChessLab.Core.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Services;

public sealed class RoomRegistry
{
    public const int OpenRoomsPerHost = 5;

    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private static readonly TimeSpan FinishedRoomLifetime = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan UnstartedRoomLifetime = TimeSpan.FromHours(2);
    private static readonly TimeSpan UnarchivedRoomLifetime = TimeSpan.FromHours(24);

    private readonly ConcurrentDictionary<string, Entry> entries = new();

    public IRoomSession CreateRoom(GameKind kind, Guid hostUserId, TimeSpan? initialClock = null,
        TimeSpan? clockIncrement = null)
    {
        while (true)
        {
            var code = GenerateCode();
            var room = new Room(code, hostUserId, kind, initialClock, clockIncrement);
            var session = CreateSession(kind, room);
            if (entries.TryAdd(code, new Entry(session, DateTimeOffset.UtcNow)))
                return session;
        }
    }

    private static IRoomSession CreateSession(GameKind kind, Room room) => kind switch
    {
        GameKind.HandAndBrain => new GameSession(room),
        GameKind.CardChess => new CardChessSession(room),
        GameKind.ArcaneChess => new ArcaneChessSession(room),
        GameKind.BiddingChess => new BiddingChessSession(room),
        GameKind.ProgressiveChess => new ProgressiveChessSession(room),
        GameKind.AliceChess => new AliceChessSession(room),
        GameKind.AbsorptionChess => new AbsorptionChessSession(room),
        GameKind.MartianChess => new MartianChessSession(room),
        GameKind.DraftChess => new DraftChessSession(room),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown game kind."),
    };

    public IRoomSession Get(string code) =>
        Find(code) ?? throw new HubException($"Room '{code}' not found.");

    public IRoomSession? Find(string code) => entries.TryGetValue(code, out var entry) ? entry.Session : null;

    public IEnumerable<IRoomSession> ActiveSessions() =>
        entries.Values.Select(entry => entry.Session).Where(session => session.HasActiveGame);

    public IReadOnlyList<IRoomSession> FinishedSessions() =>
        [.. entries.Values.Select(entry => entry.Session).Where(session => session.Game is { IsGameOver: true })];

    public int OpenRoomsHostedBy(Guid userId) =>
        entries.Values.Count(entry =>
            entry.Session.Room.HostUserId == userId && entry.Session.Game is not { IsGameOver: true });

    public void Remove(string code) => entries.TryRemove(code, out _);

    public IReadOnlyList<string> RemoveStale(DateTimeOffset now, Func<string, bool> isArchived)
    {
        var removed = new List<string>();

        foreach (var (code, entry) in entries)
        {
            var finishedLifetime = isArchived(code) ? FinishedRoomLifetime : UnarchivedRoomLifetime;
            if (entry.IsStale(now, finishedLifetime, UnstartedRoomLifetime) && entries.TryRemove(code, out _))
                removed.Add(code);
        }

        return removed;
    }

    private static string GenerateCode() =>
        new(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]).ToArray());

    private sealed class Entry(IRoomSession session, DateTimeOffset createdAt)
    {
        private DateTimeOffset? finishedSeenAt;

        public IRoomSession Session { get; } = session;

        public bool IsStale(DateTimeOffset now, TimeSpan finishedLifetime, TimeSpan unstartedLifetime)
        {
            if (Session.Game is null)
                return now - createdAt >= unstartedLifetime;

            if (!Session.Game.IsGameOver)
                return false;

            finishedSeenAt ??= now;
            return now - finishedSeenAt >= finishedLifetime;
        }
    }
}
