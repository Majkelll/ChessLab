using Microsoft.AspNetCore.SignalR;

namespace ChessLab.Web.Hubs;

public sealed class HubRateLimitFilter : IHubFilter
{
    public const int CallsPerWindow = 120;

    private const string WindowKey = "HubCallWindow";

    private static readonly TimeSpan WindowLength = TimeSpan.FromSeconds(10);

    public ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var items = invocationContext.Context.Items;
        var now = DateTimeOffset.UtcNow;

        if (!items.TryGetValue(WindowKey, out var stored) || stored is not CallWindow window ||
            now - window.StartedAt >= WindowLength)
        {
            window = new CallWindow(now);
            items[WindowKey] = window;
        }

        if (++window.Calls > CallsPerWindow)
            throw new HubException("Too many requests. Slow down.");

        return next(invocationContext);
    }

    private sealed class CallWindow(DateTimeOffset startedAt)
    {
        public DateTimeOffset StartedAt { get; } = startedAt;

        public int Calls { get; set; }
    }
}
