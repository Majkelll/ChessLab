using ChessLab.Core.Contracts;
using ChessLab.Core.Games;

namespace ChessLab.BotService;

internal sealed class BotJob
{
    private const int Queued = 0;
    private const int Started = 1;
    private const int Abandoned = 2;

    private readonly TaskCompletionSource<GameAction> completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly CancellationTokenSource queueLimit;
    private readonly CancellationTokenRegistration callerGaveUp;
    private int state = Queued;

    public BotJob(BotActionRequest request, TimeSpan queueLimitDuration, CancellationToken callerToken)
    {
        Request = request;
        queueLimit = new CancellationTokenSource(queueLimitDuration);
        queueLimit.Token.Register(() => Abandon(new QueueTimeoutException()));
        callerGaveUp = callerToken.Register(() => Abandon(new OperationCanceledException(callerToken)));
    }

    public BotActionRequest Request { get; }

    public Task<GameAction> Completion => completion.Task;

    public bool TryStart()
    {
        var started = Interlocked.CompareExchange(ref state, Started, Queued) == Queued;
        if (started)
            Release();

        return started;
    }

    public void Complete(GameAction action) => completion.TrySetResult(action);

    public void Fail(Exception exception) => completion.TrySetException(exception);

    private void Abandon(Exception reason)
    {
        if (Interlocked.CompareExchange(ref state, Abandoned, Queued) != Queued)
            return;

        completion.TrySetException(reason);
        Release();
    }

    private void Release()
    {
        callerGaveUp.Dispose();
        queueLimit.Dispose();
    }
}
