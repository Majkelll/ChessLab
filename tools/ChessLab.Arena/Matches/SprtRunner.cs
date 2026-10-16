using ChessLab.Arena.Statistics;

namespace ChessLab.Arena.Matches;

public sealed record SprtResult(SprtOutcome Outcome, MatchReport Match);

public static class SprtRunner
{
    public static async Task<SprtResult> RunAsync(Sprt sprt, MatchSettings settings, CancellationToken ct)
    {
        var limited = settings with { Pairs = Math.Max(1, sprt.GameLimit / 2) };
        var report = await MatchRunner.RunAsync(limited,
            stopWhen: match => sprt.Decide(match.Pentanomial) is SprtOutcome.H0Accepted or SprtOutcome.H1Accepted,
            ct: ct);

        var outcome = sprt.Decide(report.Pentanomial);
        return new SprtResult(outcome == SprtOutcome.Continue ? SprtOutcome.Inconclusive : outcome, report);
    }
}
