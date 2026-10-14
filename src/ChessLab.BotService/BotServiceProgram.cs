using System.Reflection;
using System.Text.Json;
using ChessLab.Bots;
using ChessLab.Core.Contracts;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChessLab.BotService;

public static class BotServiceProgram
{
    public const int MinimumApiKeyLength = 32;

    private static readonly string Version =
        typeof(BotServiceProgram).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown";

    public static void Main(string[] args) => CreateApp(args).Run();

    public static WebApplication CreateApp(string[] args, Action<WebApplicationBuilder>? configureForTests = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(BotServiceProgram).Assembly.GetName().Name,
        });
        configureForTests?.Invoke(builder);

        var apiKey = new ApiKey(RequiredApiKey(builder.Configuration));
        var workers = builder.Configuration.GetValue("BotService:Workers", Environment.ProcessorCount);
        if (workers < 1)
            throw new InvalidOperationException("BotService:Workers must be at least 1.");

        var stockfishPath = builder.Configuration["BotService:StockfishPath"] ?? "stockfish";

        builder.Services.AddSingleton(apiKey);
        builder.Services.TryAddSingleton<IChessEngineFactory>(new StockfishEngineFactory(stockfishPath));
        builder.Services.TryAddSingleton(WorkerPoolTimings.Default);
        builder.Services.AddSingleton(sp => new WorkerPool(
            workers,
            sp.GetRequiredService<IChessEngineFactory>(),
            sp.GetRequiredService<WorkerPoolTimings>(),
            sp.GetRequiredService<ILogger<WorkerPool>>()));
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WorkerPool>());

        var app = builder.Build();

        app.MapGet("/health", (WorkerPool pool) =>
            Results.Json(new BotServiceHealthDto(Version, pool.WorkerCount), BotServiceJson.Options,
                statusCode: pool.AllEnginesReady ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable));

        app.MapPost("/v1/action", ChooseActionAsync);

        return app;
    }

    private static string RequiredApiKey(IConfiguration configuration)
    {
        var key = configuration["BotService:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Missing configuration: BotService:ApiKey");
        if (key.Length < MinimumApiKeyLength)
            throw new InvalidOperationException(
                $"BotService:ApiKey must be at least {MinimumApiKeyLength} characters long.");

        return key;
    }

    private static async Task<IResult> ChooseActionAsync(HttpContext http, ApiKey apiKey, WorkerPool pool,
        ILogger<WorkerPool> logger)
    {
        if (!apiKey.Authorizes(http.Request.Headers.Authorization.ToString()))
            return Results.Unauthorized();

        if (await ReadRequestAsync(http) is not { } request || request.State.Kind != request.Kind)
            return Results.BadRequest();

        if (!SeatView.CanAct(request.State, request.Seat))
            return Results.UnprocessableEntity();

        try
        {
            var action = await pool.ChooseAsync(request, http.RequestAborted);
            return Results.Json(action, BotServiceJson.Options);
        }
        catch (QueueTimeoutException)
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            return Results.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Choosing a {Kind} action for {Seat} failed.", request.Kind, request.Seat);
            return Results.StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<BotActionRequest?> ReadRequestAsync(HttpContext http)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<BotActionRequest>(
                http.Request.Body, BotServiceJson.Options, http.RequestAborted);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
