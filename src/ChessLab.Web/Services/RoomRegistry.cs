using System.Collections.Concurrent;
using ChessLab.Core.Rooms;
using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Services;

public sealed class RoomRegistry
{
    private const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

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

    public IRoomSession? Find(string code) => sessions.TryGetValue(code, out var session) ? session : null;

    public IEnumerable<IRoomSession> ActiveSessions() => sessions.Values.Where(s => s.HasActiveGame);

    private static string GenerateCode() =>
        new(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[Random.Shared.Next(CodeAlphabet.Length)]).ToArray());
}
