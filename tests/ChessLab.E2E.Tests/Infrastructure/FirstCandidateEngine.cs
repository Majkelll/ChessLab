using ChessLab.Bots;
using ChessLab.BotService;

namespace ChessLab.E2E.Tests.Infrastructure;

internal sealed class FirstCandidateEngineFactory : IChessEngineFactory
{
    public Task<IChessEngineProcess> StartAsync(CancellationToken ct) =>
        Task.FromResult<IChessEngineProcess>(new FirstCandidateEngine());
}

internal sealed class FirstCandidateEngine : IChessEngineProcess
{
    public bool HasExited { get; private set; }

    public Task<(string BestMoveUci, int? ScoreCentipawns)> GoAsync(string fen, IReadOnlyList<string> searchMovesUci,
        int elo, int movetimeMs, CancellationToken ct = default) =>
        Task.FromResult((searchMovesUci[0], (int?)0));

    public ValueTask DisposeAsync()
    {
        HasExited = true;
        return ValueTask.CompletedTask;
    }
}

public sealed class ShiftableTimeProvider : TimeProvider
{
    private long offsetTicks;

    public void Advance(TimeSpan by) => Interlocked.Add(ref offsetTicks, by.Ticks);

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref offsetTicks));
}
