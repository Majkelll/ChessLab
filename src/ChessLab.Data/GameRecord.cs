using ChessLab.Core.Chess;
using ChessLab.Core.Rooms;

namespace ChessLab.Data;

public class GameRecord
{
    public Guid Id { get; set; }
    public string RoomCode { get; set; } = "";
    public GameKind Kind { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public GameEndReason EndReason { get; set; }
    public Side? Winner { get; set; }
    public int MoveCount { get; set; }
    public string MovesSan { get; set; } = "";
    public string FinalFen { get; set; } = "";
    public List<GameRecordPlayer> Players { get; set; } = [];
}

public class GameRecordPlayer
{
    public Guid Id { get; set; }
    public Guid GameRecordId { get; set; }
    public GameRecord? GameRecord { get; set; }
    public Side Side { get; set; }
    public SeatRole Role { get; set; }
    public Guid? UserId { get; set; }
    public string? DisplayName { get; set; }
    public BotDifficulty? BotDifficulty { get; set; }
}
