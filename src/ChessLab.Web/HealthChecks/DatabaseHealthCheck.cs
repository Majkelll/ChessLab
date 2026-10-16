using System.Diagnostics;
using ChessLab.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ChessLab.Web.HealthChecks;

internal sealed class DatabaseHealthCheck(ChessLabDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            if (!await db.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Cannot connect to the database.");
            }

            var data = new Dictionary<string, object>
            {
                ["latencyMs"] = stopwatch.ElapsedMilliseconds,
                ["provider"] = db.Database.IsNpgsql() ? "postgres" : "sqlite",
            };

            if (!db.Database.IsNpgsql())
            {
                return HealthCheckResult.Healthy("Connected.", data);
            }

            var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Count();
            var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
            data["appliedMigrations"] = appliedMigrations;
            data["pendingMigrations"] = pendingMigrations;

            return pendingMigrations == 0
                ? HealthCheckResult.Healthy("Connected.", data)
                : HealthCheckResult.Degraded("Connected, but the schema is behind the application.", data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check threw an exception.", ex);
        }
    }
}
