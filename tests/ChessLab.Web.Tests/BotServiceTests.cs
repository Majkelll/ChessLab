using System.Net;
using System.Text.Json;
using ChessLab.Core.Chess;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using ChessLab.Core.Rooms;
using ChessLab.Web.Client.Services;
using ChessLab.Web.Hubs;
using ChessLab.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace ChessLab.Web.Tests;

internal static class BotRooms
{
    public static readonly SeatId White = new(Side.White, SeatRole.Player);
    public static readonly SeatId Black = new(Side.Black, SeatRole.Player);

    public static async Task<(Player Host, string Code)> HumanAgainstBotAsync(HubApp app, GameKind kind)
    {
        var host = await app.SignInAsync("Ala");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", kind);
        await host.Connection.InvokeAsync("ClaimSeat", room.Code, White);
        await host.Connection.InvokeAsync("SetSeatBot", room.Code, Black, BotDifficulty.Easy);
        return (host, room.Code);
    }

    public static async Task PlayFirstMoveAsync(Player player, string code)
    {
        var state = await player.Connection.InvokeAsync<GameStateEnvelopeDto>("GetGameState", code);
        var move = state.AvailableMoves[0];
        await player.Connection.InvokeAsync("PerformAction", code, GameAction.MovePiece(move.From, move.To, move.PromoteTo));
    }

    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("The condition was not met within 20 seconds.");

            await Task.Delay(25);
        }
    }
}

public class BotServiceTests(BotHubApp app) : IClassFixture<BotHubApp>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        app.Bots.Reset();
        await app.BotAvailability.ProbeAsync();
    }

    public Task DisposeAsync()
    {
        app.Bots.Reset();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_bot_is_sent_exactly_the_state_its_seat_would_see()
    {
        await using var host = await app.SignInAsync("Ala");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.ArcaneChess);
        await host.Connection.InvokeAsync("ClaimSeat", room.Code, BotRooms.White);
        await host.Connection.InvokeAsync("SetSeatBot", room.Code, BotRooms.Black, BotDifficulty.Easy);
        await host.Connection.InvokeAsync("StartGame", room.Code);
        app.Bots.HoldAnswers();

        await BotRooms.PlayFirstMoveAsync(host, room.Code);
        await BotRooms.WaitUntilAsync(() => !app.Bots.Requests.IsEmpty);

        var session = app.Rooms.Get(room.Code);
        string expected;
        lock (session)
            expected = JsonSerializer.Serialize(session.ToStateDto(Side.Black), BotServiceJson.Options);

        var received = Assert.Single(app.Bots.Requests);
        using var body = JsonDocument.Parse(received.Body);
        app.Bots.ReleaseAnswers();

        Assert.Equal(expected, body.RootElement.GetProperty("state").GetRawText());
        Assert.Equal("Black", body.RootElement.GetProperty("seat").GetProperty("side").GetString());
        Assert.Empty(body.RootElement.GetProperty("state").GetProperty("arcane").GetProperty("whiteSpellHand").EnumerateArray());
        Assert.Equal($"Bearer {FakeBotService.Key}", received.Authorization);
    }

    [Fact]
    public async Task A_failing_bot_service_pauses_the_game_on_the_bots_turn_and_a_later_answer_resumes_it()
    {
        var (host, code) = await BotRooms.HumanAgainstBotAsync(app, GameKind.AliceChess);
        await using var _ = host;
        await host.Connection.InvokeAsync("StartGame", code);
        app.Bots.Failing = true;
        var pausedNotice = host.NextAsync<string>("RoomNotice", notice => notice == BotRunner.PausedNotice);
        var paused = host.NextAsync("GameUpdated", update => update.PauseDeadline is not null);

        await BotRooms.PlayFirstMoveAsync(host, code);
        await pausedNotice;
        var pausedUpdate = await paused;
        var rejection = await Assert.ThrowsAsync<HubException>(() => BotRooms.PlayFirstMoveAsync(host, code));

        var resumedNotice = host.NextAsync<string>("RoomNotice", notice => notice == BotRunner.ResumedNotice);
        var resumed = host.NextAsync("GameUpdated", update => update.PauseDeadline is null && update.MoveHistoryCount == 2);
        app.Bots.Failing = false;
        await resumedNotice;
        var resumedUpdate = await resumed;

        Assert.Equal(1, pausedUpdate.MoveHistoryCount);
        Assert.Equal("The game is paused because a bot is unavailable.", HubRejection.MessageOf(rejection));
        Assert.False(resumedUpdate.IsGameOver);
        Assert.Equal(pausedUpdate.WhiteRemainingMs, resumedUpdate.WhiteRemainingMs);
    }

    [Fact]
    public async Task A_failing_bot_service_does_not_pause_a_game_while_a_human_is_to_move()
    {
        var (host, code) = await BotRooms.HumanAgainstBotAsync(app, GameKind.AliceChess);
        await using var _ = host;
        app.Bots.Failing = true;

        await host.Connection.InvokeAsync("StartGame", code);
        await Task.Delay(1200);

        Assert.False(app.Rooms.Get(code).IsPaused);
        Assert.Empty(app.Bots.Requests);
        var moved = host.NextAsync("GameUpdated", update => update.MoveHistoryCount == 1);
        await BotRooms.PlayFirstMoveAsync(host, code);
        Assert.Equal(1, (await moved).MoveHistoryCount);
    }

    [Fact]
    public async Task Every_unlocked_room_hears_when_the_bot_service_goes_down_and_comes_back()
    {
        await using var host = await app.SignInAsync("Ala");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.CardChess);
        Assert.True(room.BotsAvailable);

        var down = host.NextAsync<RoomStateDto>("RoomUpdated", state => !state.BotsAvailable);
        app.Bots.Healthy = false;
        await app.BotAvailability.ProbeAsync();
        var downState = await down;
        var joinedWhileDown = await host.Connection.InvokeAsync<RoomStateDto?>("JoinRoom", room.Code);

        var up = host.NextAsync<RoomStateDto>("RoomUpdated", state => state.BotsAvailable);
        app.Bots.Healthy = true;
        await app.BotAvailability.ProbeAsync();
        var upState = await up;

        Assert.Equal(room.Code, downState.Code);
        Assert.False(joinedWhileDown!.BotsAvailable);
        Assert.True(upState.BotsAvailable);
    }

    [Fact]
    public async Task A_failed_action_request_makes_bots_unavailable()
    {
        var (host, code) = await BotRooms.HumanAgainstBotAsync(app, GameKind.AliceChess);
        await using var _ = host;
        await host.Connection.InvokeAsync("StartGame", code);
        app.Bots.Failing = true;

        await BotRooms.PlayFirstMoveAsync(host, code);
        await BotRooms.WaitUntilAsync(() => !app.BotAvailability.IsAvailable);

        app.Bots.Failing = false;
        await BotRooms.WaitUntilAsync(() => app.BotAvailability.IsAvailable);
    }

    [Fact]
    public async Task While_bots_are_unavailable_the_host_cannot_add_or_change_a_bot()
    {
        await using var host = await app.SignInAsync("Ala");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.CardChess);
        await host.Connection.InvokeAsync("SetSeatBot", room.Code, BotRooms.Black, BotDifficulty.Easy);
        app.Bots.Healthy = false;
        await app.BotAvailability.ProbeAsync();

        var add = await Assert.ThrowsAsync<HubException>(() =>
            host.Connection.InvokeAsync("SetSeatBot", room.Code, BotRooms.White, BotDifficulty.Easy));
        var change = await Assert.ThrowsAsync<HubException>(() =>
            host.Connection.InvokeAsync("SetSeatBot", room.Code, BotRooms.Black, BotDifficulty.Hard));
        await host.Connection.InvokeAsync("ClearSeat", room.Code, BotRooms.Black);

        Assert.Equal("Bots are unavailable right now. Try again later.", HubRejection.MessageOf(add));
        Assert.Equal("Bots are unavailable right now. Try again later.", HubRejection.MessageOf(change));
        var state = await host.Connection.InvokeAsync<RoomStateDto?>("JoinRoom", room.Code);
        Assert.All(state!.Seats, seat => Assert.Equal(OccupantKind.Empty, seat.Kind));
    }

    [Fact]
    public async Task While_bots_are_unavailable_a_game_with_a_bot_seat_cannot_start()
    {
        var (host, code) = await BotRooms.HumanAgainstBotAsync(app, GameKind.CardChess);
        await using var _ = host;
        app.Bots.Healthy = false;
        await app.BotAvailability.ProbeAsync();

        var rejection = await Assert.ThrowsAsync<HubException>(() => host.Connection.InvokeAsync("StartGame", code));

        Assert.Equal("Bots are unavailable right now. Try again later.", HubRejection.MessageOf(rejection));
        Assert.False(app.Rooms.Get(code).Room.IsLocked);
    }

    [Fact]
    public async Task While_bots_are_unavailable_a_game_between_humans_still_starts()
    {
        var (host, guest, code) = await app.TwoPlayerRoomAsync(GameKind.CardChess);
        await using var _ = host;
        await using var __ = guest;
        app.Bots.Healthy = false;
        await app.BotAvailability.ProbeAsync();

        await host.Connection.InvokeAsync("StartGame", code);

        Assert.True(app.Rooms.Get(code).HasActiveGame);
    }

    [Fact]
    public async Task The_health_report_shows_a_reachable_bot_service_with_its_version()
    {
        using var http = new HttpClient();

        var response = await http.GetAsync($"{app.BaseUrl}/hc");
        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var bots = report.RootElement.GetProperty("checks").GetProperty("bots");
        Assert.Equal("Healthy", bots.GetProperty("status").GetString());
        Assert.Equal(FakeBotService.Version, bots.GetProperty("data").GetProperty("serviceVersion").GetString());
        Assert.True(bots.GetProperty("data").GetProperty("latencyMs").GetInt64() >= 0);
    }

    [Fact]
    public async Task The_health_report_degrades_when_the_bot_service_is_unreachable()
    {
        using var http = new HttpClient();
        app.Bots.Healthy = false;
        await app.BotAvailability.ProbeAsync();

        var response = await http.GetAsync($"{app.BaseUrl}/hc");
        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", report.RootElement.GetProperty("status").GetString());
        var bots = report.RootElement.GetProperty("checks").GetProperty("bots");
        Assert.Equal("Degraded", bots.GetProperty("status").GetString());
        Assert.Equal("Bot service is unreachable.", bots.GetProperty("description").GetString());
        Assert.Empty(bots.GetProperty("data").EnumerateObject());
    }
}

public class BotPauseDeadlineTests(PauseClockHubApp app) : IClassFixture<PauseClockHubApp>
{
    [Fact]
    public async Task A_game_paused_for_five_minutes_ends_as_bot_unavailable_for_the_human_and_is_archived()
    {
        var (host, code) = await BotRooms.HumanAgainstBotAsync(app, GameKind.AliceChess);
        await using var _ = host;
        await host.Connection.InvokeAsync("StartGame", code);
        app.Bots.Failing = true;
        var paused = host.NextAsync("GameUpdated", update => update.PauseDeadline is not null);
        await BotRooms.PlayFirstMoveAsync(host, code);
        await paused;

        var ended = host.NextAsync("GameUpdated", update => update.IsGameOver);
        app.Time.Advance(GamePause.Limit);
        var update = await ended;

        Assert.Equal(GameEndReason.BotUnavailable, update.EndReason);
        Assert.Equal(Side.White, update.Winner);
        Assert.Null(update.PauseDeadline);
        IReadOnlyList<GameHistoryEntryDto> history = [];
        await BotRooms.WaitUntilAsync(() =>
        {
            history = host.Connection.InvokeAsync<IReadOnlyList<GameHistoryEntryDto>>("GetMyGameHistory", 50).GetAwaiter().GetResult();
            return history.Count == 1;
        });
        Assert.Equal(GameEndReason.BotUnavailable, history[0].EndReason);
        Assert.Equal(Side.White, history[0].Winner);
    }
}

public class NoBotServiceTests(NoBotServiceHubApp app) : IClassFixture<NoBotServiceHubApp>
{
    [Fact]
    public async Task Without_a_configured_bot_service_bots_stay_unavailable()
    {
        await using var host = await app.SignInAsync("Ala");
        var room = await host.Connection.InvokeAsync<RoomStateDto>("CreateRoom", GameKind.CardChess);

        await app.BotAvailability.ProbeAsync();
        var rejection = await Assert.ThrowsAsync<HubException>(() =>
            host.Connection.InvokeAsync("SetSeatBot", room.Code, BotRooms.Black, BotDifficulty.Easy));

        Assert.False(room.BotsAvailable);
        Assert.False(app.BotAvailability.IsAvailable);
        Assert.Equal(GameHub.BotsUnavailableRejection, HubRejection.MessageOf(rejection));
    }

    [Fact]
    public async Task The_health_report_says_the_bot_service_is_not_configured()
    {
        using var http = new HttpClient();

        using var report = JsonDocument.Parse(await http.GetStringAsync($"{app.BaseUrl}/hc"));

        var bots = report.RootElement.GetProperty("checks").GetProperty("bots");
        Assert.Equal("Degraded", bots.GetProperty("status").GetString());
        Assert.Equal("Bot service is not configured.", bots.GetProperty("description").GetString());
    }
}

public class BotServiceClientTests
{
    private static BotServiceClient ClientFor(BotServiceSettings settings) =>
        new(new HttpClient(settings.CreateHandler()), settings);

    private static BotActionRequest AliceRequest()
    {
        var room = new Room("ABCDEF", Guid.NewGuid(), GameKind.AliceChess);
        room.SetBot(BotRooms.White, BotDifficulty.Easy);
        room.SetBot(BotRooms.Black, BotDifficulty.Easy);
        var session = new AliceChessSession(room);
        session.Start(TimeSpan.FromMinutes(10), TimeSpan.Zero, DateTimeOffset.UtcNow);
        return new BotActionRequest(GameKind.AliceChess, BotRooms.White, BotDifficulty.Easy, session.ToStateDto(Side.White));
    }

    [Fact]
    public async Task The_client_sends_the_key_and_reads_the_chosen_action()
    {
        await using var service = await FakeBotService.StartAsync();
        var client = ClientFor(new BotServiceSettings(new Uri(service.BaseUrl), FakeBotService.Key, null));
        var request = AliceRequest();

        var action = await client.ChooseActionAsync(request, CancellationToken.None);

        Assert.Equal(GameActionKind.Move, action.Kind);
        Assert.Equal(request.State.AvailableMoves[0].From, action.From);
        Assert.Equal($"Bearer {FakeBotService.Key}", Assert.Single(service.Requests).Authorization);
    }

    [Fact]
    public async Task The_client_fails_when_the_service_answers_an_error()
    {
        await using var service = await FakeBotService.StartAsync();
        service.Failing = true;
        var client = ClientFor(new BotServiceSettings(new Uri(service.BaseUrl), FakeBotService.Key, null));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ChooseActionAsync(AliceRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task The_client_probe_reports_the_service_version()
    {
        await using var service = await FakeBotService.StartAsync();
        var client = ClientFor(new BotServiceSettings(new Uri(service.BaseUrl), FakeBotService.Key, null));

        var probe = await client.ProbeAsync(CancellationToken.None);

        Assert.True(probe.Succeeded);
        Assert.Equal(FakeBotService.Version, probe.ServiceVersion);
    }

    [Fact]
    public async Task The_client_accepts_a_self_signed_certificate_only_with_its_pinned_thumbprint()
    {
        using var certificate = FakeBotService.SelfSignedCertificate();
        await using var service = await FakeBotService.StartAsync(certificate);
        var url = new Uri(service.BaseUrl);
        var thumbprint = certificate.GetCertHashString(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var colonSeparated = string.Join(':', thumbprint.Chunk(2).Select(pair => new string(pair)));

        var pinned = await ClientFor(new BotServiceSettings(url, FakeBotService.Key, colonSeparated.ToLowerInvariant())).ProbeAsync(CancellationToken.None);
        var wrongPin = await ClientFor(new BotServiceSettings(url, FakeBotService.Key, new string('A', 64))).ProbeAsync(CancellationToken.None);
        var unpinned = await ClientFor(new BotServiceSettings(url, FakeBotService.Key, null)).ProbeAsync(CancellationToken.None);

        Assert.True(pinned.Succeeded);
        Assert.False(wrongPin.Succeeded);
        Assert.False(unpinned.Succeeded);
    }

    [Fact]
    public void The_settings_read_the_bot_service_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Bots:ServiceUrl"] = "https://bots.example:8443/",
                ["Bots:ServiceKey"] = "secret",
                ["Bots:ServiceCertificateThumbprint"] = "AB:CD",
            })
            .Build();

        var settings = BotServiceSettings.From(configuration);

        Assert.Equal(new Uri("https://bots.example:8443/"), settings.ServiceUrl);
        Assert.Equal("secret", settings.ServiceKey);
        Assert.Equal("AB:CD", settings.CertificateThumbprint);
        Assert.Equal(TimeSpan.FromSeconds(20), BotServiceSettings.RequestLimit);
    }
}
