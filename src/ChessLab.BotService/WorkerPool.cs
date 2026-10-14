using System.Threading.Channels;
using ChessLab.Bots;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;

namespace ChessLab.BotService;

public sealed record WorkerPoolTimings(
    TimeSpan QueueLimit,
    TimeSpan SearchLimit,
    TimeSpan EngineStartLimit,
    TimeSpan EngineRestartDelay,
    TimeSpan EngineCheckInterval)
{
    public static WorkerPoolTimings Default { get; } = new(
        QueueLimit: TimeSpan.FromSeconds(15),
        SearchLimit: TimeSpan.FromSeconds(20),
        EngineStartLimit: TimeSpan.FromSeconds(20),
        EngineRestartDelay: TimeSpan.FromSeconds(1),
        EngineCheckInterval: TimeSpan.FromSeconds(1));
}

public sealed class QueueTimeoutException() : Exception("The request waited too long for a worker.");

public sealed class EngineCrashedException(Exception inner) : Exception("The worker's engine stopped during the search.", inner);

public sealed class WorkerPool(
    int workerCount,
    IChessEngineFactory engineFactory,
    WorkerPoolTimings timings,
    ILogger<WorkerPool> logger) : BackgroundService
{
    private readonly Channel<BotJob> queue = Channel.CreateUnbounded<BotJob>();
    private readonly IChessEngineProcess?[] engines = new IChessEngineProcess?[workerCount];

    public int WorkerCount => workerCount;

    public bool AllEnginesReady
    {
        get
        {
            for (var i = 0; i < engines.Length; i++)
            {
                if (Volatile.Read(ref engines[i]) is not { HasExited: false })
                    return false;
            }

            return true;
        }
    }

    public Task<GameAction> ChooseAsync(BotActionRequest request, CancellationToken ct)
    {
        var job = new BotJob(request, timings.QueueLimit, ct);
        if (!queue.Writer.TryWrite(job))
            throw new InvalidOperationException("The worker pool is shutting down.");

        return job.Completion;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(Enumerable.Range(0, workerCount).Select(index => Task.Run(() => RunWorkerAsync(index, stoppingToken))));

    private async Task RunWorkerAsync(int index, CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var engine = await ReadyEngineAsync(index, stoppingToken);
                if (await NextJobAsync(stoppingToken) is not { } job || !job.TryStart())
                    continue;

                await RunJobAsync(index, engine, job, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await RetireEngineAsync(index);
        }
    }

    private async Task<BotJob?> NextJobAsync(CancellationToken stoppingToken)
    {
        using var check = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        check.CancelAfter(timings.EngineCheckInterval);

        try
        {
            return await queue.Reader.ReadAsync(check.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task RunJobAsync(int index, IChessEngineProcess engine, BotJob job, CancellationToken stoppingToken)
    {
        using var searchLimit = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        searchLimit.CancelAfter(timings.SearchLimit);

        try
        {
            var request = job.Request;
            var bot = GameBots.For(request.Kind, engine);
            job.Complete(await bot.ChooseActionAsync(request.State, request.Seat, request.Difficulty, searchLimit.Token));
        }
        catch (Exception ex) when (engine.HasExited || ex is OperationCanceledException)
        {
            logger.LogWarning(ex, "Worker {Worker} lost its engine during a search; starting a new one.", index);
            job.Fail(new EngineCrashedException(ex));
            await RetireEngineAsync(index);
        }
        catch (Exception ex)
        {
            job.Fail(ex);
        }
    }

    private async Task<IChessEngineProcess> ReadyEngineAsync(int index, CancellationToken stoppingToken)
    {
        if (engines[index] is { HasExited: false } running)
            return running;

        await RetireEngineAsync(index);

        while (true)
        {
            using var startLimit = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            startLimit.CancelAfter(timings.EngineStartLimit);

            try
            {
                var engine = await engineFactory.StartAsync(startLimit.Token);
                Volatile.Write(ref engines[index], engine);
                return engine;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Worker {Worker} could not start its engine.", index);
                await Task.Delay(timings.EngineRestartDelay, stoppingToken);
            }
        }
    }

    private async Task RetireEngineAsync(int index)
    {
        if (Interlocked.Exchange(ref engines[index], null) is { } engine)
            await engine.DisposeAsync();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }
}
