namespace ChessLab.Arena.Matches;

public static class MatchRunner
{
    public static int SeedOfPair(int baseSeed, int index) => unchecked((baseSeed * 1_000_003) + index);

    public static async Task<MatchReport> RunAsync(MatchSettings settings, Func<MatchReport, bool>? stopWhen = null,
        Action<PairResult>? onPair = null, CancellationToken ct = default)
    {
        if (settings.Openings.Count == 0)
            throw new ArgumentException("A match needs at least one opening.", nameof(settings));

        var report = new MatchReport(settings.Kind, settings.Candidate.Name, settings.Baseline.Name);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var next = -1;
        var gate = new object();

        async Task WorkAsync()
        {
            while (!stop.IsCancellationRequested)
            {
                var index = Interlocked.Increment(ref next);
                if (index >= settings.Pairs)
                    return;

                PairResult pair;
                try
                {
                    if (settings.BeforePair is { } wait)
                        await wait(stop.Token);

                    pair = await PlayPairAsync(settings, index, stop.Token);
                }
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    return;
                }

                lock (gate)
                {
                    if (stop.IsCancellationRequested)
                        return;

                    report.Add(pair);
                    onPair?.Invoke(pair);
                    if (stopWhen?.Invoke(report) == true)
                        stop.Cancel();
                }
            }
        }

        var workers = Enumerable.Range(0, Math.Max(1, settings.Concurrency)).Select(_ => Task.Run(WorkAsync));
        await Task.WhenAll(workers);
        ct.ThrowIfCancellationRequested();
        return report;
    }

    public static async Task<PairResult> PlayPairAsync(MatchSettings settings, int index, CancellationToken ct)
    {
        var opening = settings.Openings[index % settings.Openings.Count];
        var seed = SeedOfPair(settings.BaseSeed, index);

        var first = await GameRunner.PlayAsync(settings.Kind, opening, seed, settings.Candidate, settings.Baseline, ct);
        var second = await GameRunner.PlayAsync(settings.Kind, opening, seed, settings.Baseline, settings.Candidate, ct);

        return new PairResult(index, opening.ToString(), seed, first, second);
    }
}
