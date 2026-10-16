using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;

namespace ChessLab.BotService.Tests;

public class AuthenticationTests
{
    [Fact]
    public async Task A_request_without_a_key_is_refused_and_does_no_work()
    {
        await using var service = await BotServiceApp.StartAsync();
        using var anonymous = new HttpClient { BaseAddress = new Uri(service.BaseUrl) };

        var response = await anonymous.PostAsJsonAsync("/v1/action",
            Requests.ForActiveSeat(Requests.StartedSession(GameKind.CardChess)), BotServiceJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(service.Engines.SearchedPositions);
    }

    [Fact]
    public async Task A_request_with_the_wrong_key_is_refused()
    {
        await using var service = await BotServiceApp.StartAsync();
        using var wrong = new HttpClient { BaseAddress = new Uri(service.BaseUrl) };
        wrong.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", BotServiceApp.Key + "x");

        var response = await wrong.PostAsJsonAsync("/v1/action",
            Requests.ForActiveSeat(Requests.StartedSession(GameKind.CardChess)), BotServiceJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(service.Engines.SearchedPositions);
    }

    [Fact]
    public async Task A_request_with_the_key_under_another_scheme_is_refused()
    {
        await using var service = await BotServiceApp.StartAsync();
        using var basic = new HttpClient { BaseAddress = new Uri(service.BaseUrl) };
        basic.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", BotServiceApp.Key);

        var response = await basic.PostAsJsonAsync("/v1/action",
            Requests.ForActiveSeat(Requests.StartedSession(GameKind.CardChess)), BotServiceJson.Options);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_health_endpoint_needs_no_key()
    {
        await using var service = await BotServiceApp.StartAsync();
        using var anonymous = new HttpClient { BaseAddress = new Uri(service.BaseUrl) };

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public void The_service_does_not_start_with_a_short_key()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => BotServiceApp.Create(
            new Dictionary<string, string?> { ["BotService:ApiKey"] = new string('k', 16) }, new FakeEngineFactory()));

        Assert.Equal("BotService:ApiKey must be at least 32 characters long.", failure.Message);
    }

    [Fact]
    public void The_service_does_not_start_without_a_key()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => BotServiceApp.Create(
            new Dictionary<string, string?> { ["BotService:ApiKey"] = "" }, new FakeEngineFactory()));

        Assert.Equal("Missing configuration: BotService:ApiKey", failure.Message);
    }

    [Fact]
    public void The_api_key_check_accepts_only_the_exact_key()
    {
        var key = new ApiKey(BotServiceApp.Key);

        Assert.True(key.Authorizes($"Bearer {BotServiceApp.Key}"));
        Assert.False(key.Authorizes($"Bearer {BotServiceApp.Key[..^1]}"));
        Assert.False(key.Authorizes(""));
        Assert.False(key.Authorizes(BotServiceApp.Key));
    }
}
