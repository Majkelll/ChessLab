namespace ChessLab.Core.Rooms;

public static class RoomSessions
{
    public static IRoomSession For(Room room) => room.Kind switch
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
        _ => throw new ArgumentOutOfRangeException(nameof(room), room.Kind, "Unknown game kind."),
    };
}
