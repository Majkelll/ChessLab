using System.Net;
using ChessLab.Core.Rooms;

namespace ChessLab.BotService.Tests;

public class WorkerPoolTests
{
    [Fact]
    public async Task More_requests_than_workers_wait_their_turn_in_arrival_order()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 2);
        using var turnstile = new SemaphoreSlim(0);
        service.Engines.Turnstile = turnstile;
        var positions = Enumerable.Range(1, 6).Select(n => $"position {n}").ToArray();

        var answers = new List<Task<Core.Games.GameAction>>();
        foreach (var position in positions)
            answers.Add(service.Pool.ChooseAsync(Requests.CardChessWithPosition(position), CancellationToken.None));

        await service.WaitUntilAsync(() => service.Engines.SearchedPositions.Count == 2);
        await Task.Delay(200);
        Assert.Equal(2, service.Engines.SearchedPositions.Count);

        for (var finished = 1; finished <= 4; finished++)
        {
            turnstile.Release();
            var expected = finished + 2;
            await service.WaitUntilAsync(() => service.Engines.SearchedPositions.Count == expected);
        }

        turnstile.Release(2);
        await Task.WhenAll(answers);

        Assert.Equal(2, service.Engines.MaxActive);
        Assert.Equal(positions[..2].Order(), service.Engines.SearchedPositions.Take(2).Order());
        Assert.Equal(positions[2..], service.Engines.SearchedPositions.Skip(2));
    }

    [Fact]
    public async Task A_request_that_waited_too_long_for_a_worker_is_answered_503()
    {
        var timings = BotServiceApp.FastTimings(queueLimit: TimeSpan.FromMilliseconds(300));
        await using var service = await BotServiceApp.StartAsync(workers: 1, timings: timings);
        service.Engines.Gate = FakeEngineFactory.ClosedGate();

        var blocking = service.PostActionAsync(Requests.CardChessWithPosition("first"));
        await service.WaitUntilAsync(() => service.Engines.SearchedPositions.Count == 1);
        var waiting = await service.PostActionAsync(Requests.CardChessWithPosition("second"));
        service.Engines.Gate.SetResult();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, waiting.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await blocking).StatusCode);
        Assert.DoesNotContain("second", service.Engines.SearchedPositions);
    }

    [Fact]
    public async Task An_engine_that_dies_mid_search_fails_that_request_and_is_replaced_before_the_next()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);
        service.Engines.Gate = FakeEngineFactory.ClosedGate();

        var searching = service.PostActionAsync(Requests.CardChessWithPosition("doomed"));
        await service.WaitUntilAsync(() => service.Engines.SearchedPositions.Count == 1);
        service.Engines.Started[0].Crash();
        var failed = await searching;

        service.Engines.Gate.SetResult();
        var next = await service.PostActionAsync(Requests.CardChessWithPosition("after"));

        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        Assert.Equal(2, service.Engines.Started.Count);
        Assert.True(service.Engines.Started[0].Disposed);
    }

    [Fact]
    public async Task The_worker_count_comes_from_configuration()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 2);

        Assert.Equal(2, service.Pool.WorkerCount);
        Assert.Equal(2, service.Engines.Started.Count);
    }

    [Fact]
    public async Task Bots_without_an_engine_run_on_the_workers_too()
    {
        await using var service = await BotServiceApp.StartAsync(workers: 1);

        var response = await service.PostActionAsync(Requests.ForActiveSeat(Requests.StartedSession(GameKind.AliceChess)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(service.Engines.SearchedPositions);
    }
}
