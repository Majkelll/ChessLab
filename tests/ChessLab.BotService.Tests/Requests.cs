using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;

namespace ChessLab.BotService.Tests;

internal static class Requests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static IRoomSession StartedSession(GameKind kind)
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), kind);
        foreach (var seat in room.SeatIds)
            room.SetBot(seat, BotDifficulty.Medium);

        IRoomSession session = kind switch
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
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, Start, 3);
        return session;
    }

    public static BotActionRequest ForActiveSeat(IRoomSession session)
    {
        var seat = session.ActiveSeats[0];
        return new BotActionRequest(session.Room.Kind, seat, BotDifficulty.Medium, session.ToStateDto(seat.Side));
    }

    public static BotActionRequest CardChessWithPosition(string positionText)
    {
        var request = ForActiveSeat(StartedSession(GameKind.CardChess));
        return request with { State = request.State with { PositionText = positionText } };
    }

    public static DateTimeOffset Now => Start;

    public static Side Opposite(Side side) => side.Opposite();
}
