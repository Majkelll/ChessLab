using System.Net.Http.Json;
using ChessLab.Core.Contracts;

namespace ChessLab.Arena.Matches;

public sealed class BotQueueGate(HttpClient http, Uri queueUrl, TimeSpan pollInterval, TextWriter? log = null)
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

    public async Task WaitUntilIdleAsync(CancellationToken ct)
    {
        var announced = false;
        while (true)
        {
            var queue = await http.GetFromJsonAsync<BotServiceQueueDto>(queueUrl, BotServiceJson.Options, ct)
                ?? throw new InvalidOperationException($"{queueUrl} answered with an empty body.");

            if (queue.Queued + queue.Running == 0)
                return;

            if (!announced)
            {
                log?.WriteLine($"Bot service busy ({queue.Queued} queued, {queue.Running} running), waiting");
                announced = true;
            }

            await Task.Delay(pollInterval, ct);
        }
    }
}
