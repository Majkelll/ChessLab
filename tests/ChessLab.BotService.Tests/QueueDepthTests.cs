using System.Net;
using System.Net.Http.Json;
using ChessLab.BotService;
using ChessLab.Core.Contracts;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace ChessLab.BotService.Tests;

public class QueueDepthTests
{
    [Fact]
    public async Task An_idle_service_reports_an_empty_queue()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 2);

        var queue = await service.Http.GetFromJsonAsync<BotServiceQueueDto>("/internal/queue", BotServiceJson.Options);

        Assert.Equal(new BotServiceQueueDto(0, 0), queue);
    }

    [Fact]
    public async Task A_busy_service_reports_the_queued_and_running_requests()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);
        service.Engines.Gate = FakeEngineFactory.ClosedGate();
        var answers = Enumerable.Range(1, 3)
            .Select(n => service.Pool.ChooseAsync(Requests.CardChessWithPosition($"position {n}"), CancellationToken.None))
            .ToArray();
        await service.WaitUntilAsync(() => service.Engines.SearchedPositions.Count == 1);

        var queue = await service.Http.GetFromJsonAsync<BotServiceQueueDto>("/internal/queue", BotServiceJson.Options);
        service.Engines.Gate.SetResult();
        await Task.WhenAll(answers);

        Assert.Equal(new BotServiceQueueDto(2, 1), queue);
    }

    [Fact]
    public async Task The_queue_is_hidden_on_every_port_but_the_internal_one()
    {
        var app = BotServiceApp.Create(new Dictionary<string, string?>
        {
            ["BotService:ApiKey"] = BotServiceApp.Key,
            ["BotService:Workers"] = "1",
            ["BotService:InternalPort"] = "1",
        }, new FakeEngineFactory());
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            using var http = new HttpClient { BaseAddress = new Uri(address) };

            var response = await http.GetAsync("/internal/queue");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
