using ChessLab.Core.Chess;

namespace ChessLab.Arena.Matches;

public enum Forfeit
{
    IllegalAction,
    Crash,
    TimeOverrun,
}

public sealed record GameRecord(Side? Winner, string Reason, Forfeit? Forfeit, int Actions, string FinalPosition)
{
    public static string Describe(Forfeit forfeit) => forfeit switch
    {
        Matches.Forfeit.IllegalAction => "illegal action",
        Matches.Forfeit.Crash => "crash",
        Matches.Forfeit.TimeOverrun => "time overrun",
        _ => throw new ArgumentOutOfRangeException(nameof(forfeit)),
    };

    public double PointsFor(Side side) => Winner is null ? 0.5 : Winner == side ? 1 : 0;
}
