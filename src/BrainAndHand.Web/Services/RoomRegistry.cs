using System.Collections.Concurrent;
using BrainAndHand.Core.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace BrainAndHand.Web.Services;

/// <summary>Live, in-memory room/game state for this process. No persistence — matches ongoing games only.</summary>
public sealed class RoomRegistry
{
    // Excludes 0/O/1/I/L to avoid ambiguous invite codes.
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly ConcurrentDictionary<string, GameSession> sessions = new();

    public GameSession CreateRoom(Guid hostUserId, TimeSpan? initialClock = null, TimeSpan? clockIncrement = null)
    {
        while (true)
        {
            var code = GenerateCode();
            var session = new GameSession(new Room(code, hostUserId, initialClock, clockIncrement));
            if (sessions.TryAdd(code, session))
                return session;
        }
    }

    public GameSession Get(string code)
    {
        if (!sessions.TryGetValue(code, out var session))
            throw new HubException($"Room '{code}' not found.");

        return session;
    }

    /// <summary>Sessions with a game currently in progress — used by the clock watchdog.</summary>
    public IEnumerable<GameSession> ActiveSessions() => sessions.Values.Where(s => s.Game is { IsGameOver: false });

    private static string GenerateCode() =>
        new(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]).ToArray());
}
