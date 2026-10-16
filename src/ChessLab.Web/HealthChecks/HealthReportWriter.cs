using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ChessLab.Web.HealthChecks;

internal static class HealthReportWriter
{
    private static readonly string Version =
        typeof(HealthReportWriter).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var response = new HealthResponse(
            report.Status.ToString(),
            Version,
            (long)uptime.TotalSeconds,
            (long)report.TotalDuration.TotalMilliseconds,
            report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new HealthCheckEntry(
                    entry.Value.Status.ToString(),
                    entry.Value.Description,
                    (long)entry.Value.Duration.TotalMilliseconds,
                    entry.Value.Data)));

        return context.Response.WriteAsJsonAsync(response);
    }

    private sealed record HealthResponse(
        string Status,
        string Version,
        long UptimeSeconds,
        long DurationMs,
        IReadOnlyDictionary<string, HealthCheckEntry> Checks);

    private sealed record HealthCheckEntry(
        string Status,
        string? Description,
        long DurationMs,
        IReadOnlyDictionary<string, object> Data);
}
