using ChessLab.Bots;
using ChessLab.BotService;

namespace ChessLab.BotService.Tests;

internal sealed class FakeEngineFactory : IChessEngineFactory
{
    private readonly object sync = new();
    private readonly List<string> searchedPositions = [];
    private readonly List<FakeEngine> started = [];
    private int active;
    private int maxActive;

    public TaskCompletionSource Gate { get; set; } = CompletedGate();

    public SemaphoreSlim? Turnstile { get; set; }

    public bool FailStarts { get; set; }

    public int MaxActive => Volatile.Read(ref maxActive);

    public IReadOnlyList<string> SearchedPositions
    {
        get
        {
            lock (sync)
                return [.. searchedPositions];
        }
    }

    public IReadOnlyList<FakeEngine> Started
    {
        get
        {
            lock (sync)
                return [.. started];
        }
    }

    public static TaskCompletionSource CompletedGate()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        gate.SetResult();
        return gate;
    }

    public static TaskCompletionSource ClosedGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<IChessEngineProcess> StartAsync(CancellationToken ct)
    {
        if (FailStarts)
            throw new InvalidOperationException("No engine binary.");

        var engine = new FakeEngine(this);
        lock (sync)
            started.Add(engine);

        return Task.FromResult<IChessEngineProcess>(engine);
    }

    internal void SearchStarted(string fen)
    {
        lock (sync)
            searchedPositions.Add(fen);

        var now = Interlocked.Increment(ref active);
        int seen;
        while (now > (seen = Volatile.Read(ref maxActive)) &&
               Interlocked.CompareExchange(ref maxActive, now, seen) != seen)
        {
        }
    }

    internal void SearchEnded() => Interlocked.Decrement(ref active);
}

internal sealed class FakeEngine(FakeEngineFactory factory) : IChessEngineProcess
{
    private readonly TaskCompletionSource crashed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool HasExited => crashed.Task.IsCompleted;

    public bool Disposed { get; private set; }

    public void Crash() => crashed.TrySetResult();

    public async Task<(string BestMoveUci, int? ScoreCentipawns)> GoAsync(string fen, IReadOnlyList<string> searchMovesUci,
        int elo, int movetimeMs, CancellationToken ct = default)
    {
        factory.SearchStarted(fen);
        try
        {
            if (factory.Turnstile is { } turnstile)
                await Task.WhenAny(turnstile.WaitAsync(ct), crashed.Task);

            await Task.WhenAny(factory.Gate.Task, crashed.Task).WaitAsync(ct);
            if (HasExited)
                throw new IOException("Stockfish process ended unexpectedly.");

            return (searchMovesUci[0], 0);
        }
        finally
        {
            factory.SearchEnded();
        }
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        crashed.TrySetResult();
        return ValueTask.CompletedTask;
    }
}
