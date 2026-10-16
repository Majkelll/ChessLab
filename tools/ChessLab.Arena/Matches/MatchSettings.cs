using ChessLab.Arena.Notation;
using ChessLab.Arena.Players;
using ChessLab.Core.Rooms;

namespace ChessLab.Arena.Matches;

public sealed record MatchSettings(
    GameKind Kind,
    IPlayerFactory Candidate,
    IPlayerFactory Baseline,
    IReadOnlyList<PositionLine> Openings,
    int Pairs,
    int Concurrency,
    int BaseSeed = 1,
    Func<CancellationToken, Task>? BeforePair = null);
