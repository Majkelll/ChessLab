using System.Net;
using System.Net.Http.Json;
using ChessLab.Arena.Matches;
using ChessLab.Arena.Notation;
using ChessLab.Core.Contracts;
using ChessLab.Core.Rooms;

namespace ChessLab.Engine.Tests.Matches;

public class BotQueueGateTests
{
    [Fact]
    public async Task BusyBotService_HoldsTheGateUntilItsQueueIsEmpty()
    {
        var answers = new Queue<BotServiceQueueDto>([new(2, 4), new(0, 1), new(0, 0)]);
        var handler = new QueueHandler(answers);
        var gate = new BotQueueGate(new HttpClient(handler), new Uri("http://127.0.0.1:51822/internal/queue"),
            TimeSpan.FromMilliseconds(10));

        await gate.WaitUntilIdleAsync(CancellationToken.None);

        Assert.Equal(3, handler.Requests);
        Assert.Empty(answers);
    }

    [Fact]
    public async Task Match_WaitsAtTheGateBeforeEveryPair()
    {
        var waits = 0;
        var settings = new MatchSettings(GameKind.MartianChess, FakePlayer.First(), FakePlayer.First(),
            [PositionLine.Start()], Pairs: 3, Concurrency: 1,
            BeforePair: _ =>
            {
                Interlocked.Increment(ref waits);
                return Task.CompletedTask;
            });

        await MatchRunner.RunAsync(settings);

        Assert.Equal(3, waits);
    }

    private sealed class QueueHandler(Queue<BotServiceQueueDto> answers) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(answers.Dequeue(), options: BotServiceJson.Options),
            });
        }
    }
}
