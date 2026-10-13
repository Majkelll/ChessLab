using System.Net;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChessLab.Web.Tests;

public class HubApp : IAsyncLifetime
{
    private WebApplication? app;
    private string? dbPath;

    protected virtual int InitialClockSeconds => 600;

    public string BaseUrl { get; private set; } = "";

    public IServiceProvider Services => app!.Services;

    public async Task InitializeAsync()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"chesslab-web-tests-{Guid.NewGuid():N}.db");

        app = Program.CreateApp([], environment: "Development", configureForTests: builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={dbPath}",
                ["E2E_TEST_AUTH"] = "true",
                ["Authentication:Google:ClientId"] = "web-tests.apps.googleusercontent.com",
                ["Authentication:Google:ClientSecret"] = "web-tests-secret",
                ["Game:InitialClockSeconds"] = InitialClockSeconds.ToString(),
            });
        });

        await app.StartAsync();

        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!;
        BaseUrl = addresses.Addresses.First().TrimEnd('/');
    }

    public async Task DisposeAsync()
    {
        if (app is not null)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        foreach (var suffix in new[] { "", "-shm", "-wal" })
            File.Delete(dbPath + suffix);
    }

    public async Task<Player> SignInAsync(string name)
    {
        var userId = Guid.NewGuid();
        var cookies = new CookieContainer();
        using var http = new HttpClient(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false });
        await http.GetAsync($"{BaseUrl}/TestAuth/Login?userId={userId}&name={name}");

        var connection = new HubConnectionBuilder()
            .WithUrl($"{BaseUrl}/hubs/game", options => options.Cookies = cookies)
            .AddMessagePackProtocol()
            .Build();
        await connection.StartAsync();

        return new Player(userId, connection);
    }

    public async Task<(Player Host, Player Guest, string Code)> TwoPlayerRoomAsync(GameKind kind)
    {
        var host = await SignInAsync("Ala");
        var guest = await SignInAsync("Bob");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", kind);
        await guest.Connection.InvokeAsync<RoomStateDto?>("JoinRoom", room.Code);
        await host.Connection.InvokeAsync("ClaimSeat", room.Code, new SeatId(Side.White, SeatRole.Player));
        await guest.Connection.InvokeAsync("ClaimSeat", room.Code, new SeatId(Side.Black, SeatRole.Player));
        return (host, guest, room.Code);
    }
}

public sealed class ShortClockHubApp : HubApp
{
    protected override int InitialClockSeconds => 1;
}

public sealed class Player(Guid userId, HubConnection connection) : IAsyncDisposable
{
    public Guid UserId { get; } = userId;

    public HubConnection Connection { get; } = connection;

    public Task<GameUpdateEnvelopeDto> NextAsync(string eventName, Func<GameUpdateEnvelopeDto, bool>? wanted = null)
    {
        var received = new TaskCompletionSource<GameUpdateEnvelopeDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        Connection.On<GameUpdateEnvelopeDto>(eventName, update =>
        {
            if (wanted is null || wanted(update))
                received.TrySetResult(update);
        });
        return received.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}

[CollectionDefinition(Name)]
public sealed class HubAppCollection : ICollectionFixture<HubApp>
{
    public const string Name = "Hub app";
}
