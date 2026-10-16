using System.Globalization;
using System.Net;
using System.Net.Sockets;
using ChessLab.BotService;
using ChessLab.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChessLab.E2E.Tests.Infrastructure;

public class WebAppFixture : IAsyncLifetime
{
    private const string BotServiceKey = "e2e-bot-service-key-0123456789-0123456789";

    private WebApplication? app;
    private WebApplication? botService;
    private string? dbPath;
    private int botServicePort;

    protected virtual double InitialClockSeconds => 600;
    protected virtual double ClockIncrementSeconds => 0;

    public string BaseUrl { get; private set; } = "";

    public string? StockfishPath { get; private set; }

    public bool HasStockfish => StockfishPath is not null;

    public ShiftableTimeProvider Time { get; } = new();

    public virtual async Task InitializeAsync()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"chesslab-e2e-{Guid.NewGuid():N}.db");
        StockfishPath = StockfishLocator.Find();
        botServicePort = FreePort();
        await StartBotServiceAsync();

        app = Program.CreateApp([], environment: "Development", configureForTests: builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={dbPath}",
                ["E2E_TEST_AUTH"] = "true",
                ["Authentication:Google:ClientId"] = "e2e-test.apps.googleusercontent.com",
                ["Authentication:Google:ClientSecret"] = "e2e-test-secret",
                ["Bots:ServiceUrl"] = $"http://127.0.0.1:{botServicePort}/",
                ["Bots:ServiceKey"] = BotServiceKey,
                ["Game:InitialClockSeconds"] = InitialClockSeconds.ToString(CultureInfo.InvariantCulture),
                ["Game:ClockIncrementSeconds"] = ClockIncrementSeconds.ToString(CultureInfo.InvariantCulture),
            });
            builder.Services.AddSingleton<TimeProvider>(Time);
        });

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Server did not report a listening address.");
        BaseUrl = addresses.Addresses.First().Replace("[::]", "127.0.0.1", StringComparison.Ordinal).TrimEnd('/');
        await RefreshBotAvailabilityAsync();
    }

    public async Task StartBotServiceAsync()
    {
        botService = BotServiceProgram.CreateApp([], builder =>
        {
            builder.WebHost.UseUrls($"http://127.0.0.1:{botServicePort}");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BotService:ApiKey"] = BotServiceKey,
                ["BotService:Workers"] = "2",
                ["BotService:StockfishPath"] = StockfishPath ?? "stockfish",
            });
            if (StockfishPath is null)
                builder.Services.AddSingleton<IChessEngineFactory>(new FirstCandidateEngineFactory());
        });

        await botService.StartAsync();
        var pool = botService.Services.GetRequiredService<WorkerPool>();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!pool.AllEnginesReady && DateTime.UtcNow < deadline)
            await Task.Delay(50);
    }

    public async Task StopBotServiceAsync()
    {
        if (botService is null)
            return;

        await botService.StopAsync();
        await botService.DisposeAsync();
        botService = null;
    }

    public Task RefreshBotAvailabilityAsync() =>
        app!.Services.GetRequiredService<BotAvailability>().ProbeAsync();

    public virtual async Task DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        await StopBotServiceAsync();

        if (dbPath is not null)
        {
            TryDelete(dbPath);
            TryDelete(dbPath + "-shm");
            TryDelete(dbPath + "-wal");
        }
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}

public sealed class BotOutageWebAppFixture : WebAppFixture;
