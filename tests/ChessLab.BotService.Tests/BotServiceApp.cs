using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChessLab.BotService;
using ChessLab.Core.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChessLab.BotService.Tests;

internal sealed class BotServiceApp : IAsyncDisposable
{
    public const string Key = "test-key-0123456789-0123456789-0123456789";

    private readonly WebApplication app;

    private BotServiceApp(WebApplication app, FakeEngineFactory engines, string baseUrl)
    {
        this.app = app;
        Engines = engines;
        BaseUrl = baseUrl;
        Http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Key);
    }

    public FakeEngineFactory Engines { get; }

    public string BaseUrl { get; }

    public HttpClient Http { get; }

    public WorkerPool Pool => app.Services.GetRequiredService<WorkerPool>();

    public static WebApplication Create(IDictionary<string, string?> settings, FakeEngineFactory engines,
        WorkerPoolTimings? timings = null) =>
        BotServiceProgram.CreateApp([], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(settings);
            builder.Services.AddSingleton<IChessEngineFactory>(engines);
            builder.Services.AddSingleton(timings ?? FastTimings());
        });

    public static WorkerPoolTimings FastTimings(TimeSpan? queueLimit = null) => new(
        QueueLimit: queueLimit ?? TimeSpan.FromSeconds(15),
        SearchLimit: TimeSpan.FromSeconds(20),
        EngineStartLimit: TimeSpan.FromSeconds(5),
        EngineRestartDelay: TimeSpan.FromMilliseconds(50),
        EngineCheckInterval: TimeSpan.FromMilliseconds(50));

    public static async Task<BotServiceApp> StartAsync(int workers = 2, WorkerPoolTimings? timings = null,
        FakeEngineFactory? engines = null)
    {
        engines ??= new FakeEngineFactory();
        var app = Create(new Dictionary<string, string?>
        {
            ["BotService:ApiKey"] = Key,
            ["BotService:Workers"] = workers.ToString(),
        }, engines, timings);

        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        var started = new BotServiceApp(app, engines, addresses.Addresses.First().TrimEnd('/'));
        await started.WaitUntilAsync(() => started.Pool.AllEnginesReady);
        return started;
    }

    public Task<HttpResponseMessage> PostActionAsync(BotActionRequest request) =>
        Http.PostAsJsonAsync("/v1/action", request, BotServiceJson.Options);

    public async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The condition was not met within 10 seconds.");

            await Task.Delay(20);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await app.StopAsync();
        await app.DisposeAsync();
    }
}
