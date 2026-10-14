using System.Net;
using System.Net.Http.Json;
using ChessLab.Core.Contracts;

namespace ChessLab.BotService.Tests;

public class HealthTests
{
    [Fact]
    public async Task A_service_whose_engines_all_run_reports_its_version_and_workers()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 3);

        var response = await service.Http.GetAsync("/health");
        var health = await response.Content.ReadFromJsonAsync<BotServiceHealthDto>(BotServiceJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, health!.Workers);
        Assert.False(string.IsNullOrWhiteSpace(health.Version));
        Assert.Equal(3, service.Engines.Started.Count);
    }

    [Fact]
    public async Task A_service_that_cannot_start_an_engine_reports_unhealthy()
    {
        var engines = new FakeEngineFactory { FailStarts = true };
        await using var app = new AppWithoutWaiting(engines);
        await app.StartAsync();

        var response = await app.Http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task A_service_whose_engine_exited_reports_unhealthy_until_it_is_restarted()
    {
        var timings = BotServiceApp.FastTimings() with { EngineCheckInterval = TimeSpan.FromMilliseconds(300) };
        await using var service = await BotServiceApp.StartAsync(workers: 1, timings: timings);

        service.Engines.Started[0].Crash();
        var whileDown = await service.Http.GetAsync("/health");
        await service.WaitUntilAsync(() => service.Engines.Started.Count == 2 && service.Pool.AllEnginesReady);
        var afterRestart = await service.Http.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, whileDown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterRestart.StatusCode);
        Assert.True(service.Engines.Started[0].Disposed);
    }

    private sealed class AppWithoutWaiting(FakeEngineFactory engines) : IAsyncDisposable
    {
        private readonly Microsoft.AspNetCore.Builder.WebApplication app = BotServiceApp.Create(
            new Dictionary<string, string?> { ["BotService:ApiKey"] = BotServiceApp.Key, ["BotService:Workers"] = "1" },
            engines);

        public HttpClient Http { get; private set; } = new();

        public async Task StartAsync()
        {
            await app.StartAsync();
            Http = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };
        }

        public async ValueTask DisposeAsync()
        {
            Http.Dispose();
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
