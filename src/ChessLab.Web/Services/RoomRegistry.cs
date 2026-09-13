using System.Collections.Concurrent;
using ChessLab.Core.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Services;

/// <summary>Live, in-memory room/game state for this process. No persistence — matches ongoing games only.</summary>
public sealed class RoomRegistry
{
    // Excludes 0/O/1/I/L to avoid ambiguous invite codes.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    // Both game kinds share one code space — a code is unique regardless of which game it's for.
    private readonly ConcurrentDictionary<string, IRoomSession> sessions = new();

    public GameSession CreateRoom(Guid hostUserId, TimeSpan? initialClock = null, TimeSpan? clockIncrement = null)
    {
        while (true)
        {
            var code = GenerateCode();
            var session = new GameSession(new Room(code, hostUserId, GameKind.HandAndBrain, initialClock, clockIncrement));
            if (sessions.TryAdd(code, session))
                return session;
        }
    }

    public CardChessSession CreateCardChessRoom(Guid hostUserId, TimeSpan? initialClock = null, TimeSpan? clockIncrement = null)
    {
        while (true)
        {
            var code = GenerateCode();
            var session = new CardChessSession(new Room(code, hostUserId, GameKind.CardChess, initialClock, clockIncrement));
            if (sessions.TryAdd(code, session))
                return session;
        }
    }

    public ArcaneChessSession CreateArcaneChessRoom(Guid hostUserId, TimeSpan? initialClock = null, TimeSpan? clockIncrement = null)
    {
        while (true)
        {
            var code = GenerateCode();
            var session = new ArcaneChessSession(new Room(code, hostUserId, GameKind.ArcaneChess, initialClock, clockIncrement));
            if (sessions.TryAdd(code, session))
                return session;
        }
    }

    public GameSession Get(string code)
    {
        if (GetAny(code) is not GameSession handBrain)
            throw new HubException($"Room '{code}' is not a Hand & Brain room.");

        return handBrain;
    }

    public CardChessSession GetCardChess(string code)
    {
        if (GetAny(code) is not CardChessSession cardChess)
            throw new HubException($"Room '{code}' is not a Card Chess room.");

        return cardChess;
    }

    public ArcaneChessSession GetArcaneChess(string code)
    {
        if (GetAny(code) is not ArcaneChessSession arcaneChess)
            throw new HubException($"Room '{code}' is not an Arcane Chess room.");

        return arcaneChess;
    }

    /// <summary>Kind-agnostic lookup for room-management operations (join, seats, clock settings)
    /// that behave the same regardless of which game a room is for.</summary>
    public IRoomSession GetAny(string code)
    {
        if (!sessions.TryGetValue(code, out var session))
            throw new HubException($"Room '{code}' not found.");

        return session;
    }

    /// <summary>Lookup that treats a missing room as an expected answer rather than a failure:
    /// rooms only live in memory, so any code a player still has stops resolving once the server
    /// restarts, and the client turns that into a "this room is gone" screen instead of an error.</summary>
    public IRoomSession? FindAny(string code) =>
        sessions.TryGetValue(code, out var session) ? session : null;

    /// <summary>Hand &amp; Brain sessions with a game currently in progress — used by the clock watchdog.</summary>
    public IEnumerable<GameSession> ActiveSessions() =>
        sessions.Values.OfType<GameSession>().Where(s => s.HasActiveGame);

    /// <summary>Card Chess sessions with a game currently in progress — used by the clock watchdog.</summary>
    public IEnumerable<CardChessSession> ActiveCardChessSessions() =>
        sessions.Values.OfType<CardChessSession>().Where(s => s.HasActiveGame);

    /// <summary>Arcane Chess sessions with a game currently in progress — used by the clock watchdog.</summary>
    public IEnumerable<ArcaneChessSession> ActiveArcaneChessSessions() =>
        sessions.Values.OfType<ArcaneChessSession>().Where(s => s.HasActiveGame);

    private static string GenerateCode() =>
        new(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]).ToArray());
}
