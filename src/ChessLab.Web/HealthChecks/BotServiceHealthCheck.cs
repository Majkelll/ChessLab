using ChessLab.Web.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ChessLab.Web.HealthChecks;

internal sealed class BotServiceHealthCheck(BotAvailability availability) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!availability.IsConfigured)
            return Task.FromResult(HealthCheckResult.Degraded("Bot service is not configured."));

        if (availability.LatestProbe is not { Succeeded: true } probe)
            return Task.FromResult(HealthCheckResult.Degraded("Bot service is unreachable."));

        var data = new Dictionary<string, object>
        {
            ["latencyMs"] = probe.LatencyMs,
            ["serviceVersion"] = probe.ServiceVersion ?? "unknown",
        };
        return Task.FromResult(HealthCheckResult.Healthy("Reachable.", data));
    }
}
