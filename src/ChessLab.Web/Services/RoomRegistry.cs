using System.Collections.Concurrent;
using ChessLab.Core.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Services;

/// <summary>Live, in-memory room/game state for this process. No persistence — matches ongoing games only.</summary>
public sealed class RoomRegistry
{
    // Excludes 0/O/1/I/L to avoid ambiguous invite codes.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    // Every game kind shares one code space — a code is unique regardless of which game it's for.
    private readonly ConcurrentDictionary<string, IRoomSession> sessions = new();

    public IRoomSession CreateRoom(GameKind kind, Guid hostUserId, TimeSpan? initialClock = null,
        TimeSpan? clockIncrement = null)
    {
        while (true)
        {
            var code = GenerateCode();
            var room = new Room(code, hostUserId, kind, initialClock, clockIncrement);
            var session = CreateSession(kind, room);
            if (sessions.TryAdd(code, session))
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

    public IRoomSession Get(string code)
    {
        if (!sessions.TryGetValue(code, out var session))
            throw new HubException($"Room '{code}' not found.");

        return session;
    }

    /// <summary>Lookup that treats a missing room as an expected answer rather than a failure:
    /// rooms only live in memory, so any code a player still has stops resolving once the server
    /// restarts, and the client turns that into a "this room is gone" screen instead of an error.</summary>
    public IRoomSession? Find(string code) => sessions.TryGetValue(code, out var session) ? session : null;

    /// <summary>Sessions with a game currently in progress — used by the clock watchdog.</summary>
    public IEnumerable<IRoomSession> ActiveSessions() => sessions.Values.Where(s => s.HasActiveGame);

    private static string GenerateCode() =>
        new(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]).ToArray());
}
