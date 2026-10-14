namespace ChessLab.Core.Rooms;

public static class GamePause
{
    public const string Rejection = "The game is paused because a bot is unavailable.";

    public static TimeSpan Limit { get; } = TimeSpan.FromMinutes(5);
}
