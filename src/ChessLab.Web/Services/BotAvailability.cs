namespace ChessLab.Web.Services;

public sealed class BotAvailability(
    IServiceScopeFactory scopeFactory,
    BotServiceSettings settings,
    ILogger<BotAvailability> logger) : BackgroundService
{
    public static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(10);

    private readonly SemaphoreSlim probing = new(1, 1);
    private int available;

    public event Action? Changed;

    public bool IsConfigured => settings.IsConfigured;

    public bool IsAvailable => Volatile.Read(ref available) == 1;

    public BotServiceProbe? LatestProbe { get; private set; }

    public async Task ProbeAsync(CancellationToken ct = default)
    {
        if (!IsConfigured)
            return;

        await probing.WaitAsync(ct);
        try
        {
            using var scope = scopeFactory.CreateScope();
            var probe = await scope.ServiceProvider.GetRequiredService<BotServiceClient>().ProbeAsync(ct);
            LatestProbe = probe;
            Report(probe.Succeeded);
        }
        finally
        {
            probing.Release();
        }
    }

    public void Report(bool succeeded)
    {
        var now = succeeded ? 1 : 0;
        if (Interlocked.Exchange(ref available, now) == now)
            return;

        logger.LogInformation("The bot service is now {State}.", succeeded ? "available" : "unavailable");
        Changed?.Invoke();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsConfigured)
        {
            logger.LogWarning("No bot service is configured; bots are unavailable.");
            return;
        }

        using var timer = new PeriodicTimer(ProbeInterval);
        do
        {
            try
            {
                await ProbeAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Probing the bot service failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
