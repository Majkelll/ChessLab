using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChessLab.E2E.Tests.Infrastructure;

public class WebAppFixture : IAsyncLifetime
{
    private WebApplication? app;
    private string? dbPath;

    protected virtual double InitialClockSeconds => 600;
    protected virtual double ClockIncrementSeconds => 0;

    public string BaseUrl { get; private set; } = "";

    public string? StockfishPath { get; private set; }

    public bool HasStockfish => StockfishPath is not null;

    public virtual async Task InitializeAsync()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"chesslab-e2e-{Guid.NewGuid():N}.db");
        StockfishPath = StockfishLocator.Find();

        app = Program.CreateApp([], environment: "Development", configureForTests: builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={dbPath}",
                ["E2E_TEST_AUTH"] = "true",
                ["Authentication:Google:ClientId"] = "e2e-test.apps.googleusercontent.com",
                ["Authentication:Google:ClientSecret"] = "e2e-test-secret",
                ["Bots:StockfishPath"] = StockfishPath ?? "stockfish",
                ["Game:InitialClockSeconds"] = InitialClockSeconds.ToString(CultureInfo.InvariantCulture),
                ["Game:ClockIncrementSeconds"] = ClockIncrementSeconds.ToString(CultureInfo.InvariantCulture),
            });
        });

        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Server did not report a listening address.");
        BaseUrl = addresses.Addresses.First().Replace("[::]", "127.0.0.1", StringComparison.Ordinal).TrimEnd('/');
    }

    public virtual async Task DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        if (dbPath is not null)
        {
            TryDelete(dbPath);
            TryDelete(dbPath + "-shm");
            TryDelete(dbPath + "-wal");
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }
}
