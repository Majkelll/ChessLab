using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using ChessLab.Core.Bidding;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChessLab.Web.Tests;

public sealed record ReceivedBotRequest(string? Authorization, string Body);

public sealed class FakeBotService : IAsyncDisposable
{
    public const string Key = "web-tests-key-0123456789-0123456789";
    public const string Version = "fake-1";

    private readonly WebApplication app;
    private volatile bool healthy = true;
    private volatile bool failing;
    private volatile TaskCompletionSource? hold;

    private FakeBotService(WebApplication app) => this.app = app;

    public string BaseUrl { get; private set; } = "";

    public ConcurrentQueue<ReceivedBotRequest> Requests { get; } = new();

    public bool Healthy { get => healthy; set => healthy = value; }

    public bool Failing { get => failing; set => failing = value; }

    public void HoldAnswers() => hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    public void ReleaseAnswers()
    {
        hold?.TrySetResult();
        hold = null;
    }

    public void Reset()
    {
        Healthy = true;
        Failing = false;
        ReleaseAnswers();
        Requests.Clear();
    }

    public static async Task<FakeBotService> StartAsync(X509Certificate2? certificate = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen =>
        {
            if (certificate is not null)
                listen.UseHttps(certificate);
        }));

        var service = new FakeBotService(builder.Build());
        service.app.MapGet("/health", () => service.Healthy
            ? Results.Json(new BotServiceHealthDto(Version, 1), BotServiceJson.Options)
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));
        service.app.MapPost("/v1/action", service.AnswerAsync);

        await service.app.StartAsync();
        var address = service.app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        service.BaseUrl = address.TrimEnd('/') + "/";
        return service;
    }

    private async Task<IResult> AnswerAsync(HttpRequest http)
    {
        using var reader = new StreamReader(http.Body);
        var body = await reader.ReadToEndAsync();
        Requests.Enqueue(new ReceivedBotRequest(http.Headers.Authorization.ToString(), body));

        if (hold is { } gate)
            await gate.Task;

        if (Failing)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        var request = JsonSerializer.Deserialize<BotActionRequest>(body, BotServiceJson.Options)!;
        return ActionFor(request.State) is { } action
            ? Results.Json(action, BotServiceJson.Options)
            : Results.UnprocessableEntity();
    }

    private static GameAction? ActionFor(GameStateEnvelopeDto state)
    {
        if (state.Bidding is { Phase: BiddingPhase.Bidding })
            return GameAction.SubmitBid(0);

        return state.AvailableMoves.Count > 0
            ? GameAction.MovePiece(state.AvailableMoves[0].From, state.AvailableMoves[0].To, state.AvailableMoves[0].PromoteTo)
            : null;
    }

    public static X509Certificate2 SelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddIpAddress(IPAddress.Loopback);
        names.AddDnsName("localhost");
        request.CertificateExtensions.Add(names.Build());

        using var created = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return X509CertificateLoader.LoadPkcs12(created.Export(X509ContentType.Pfx), null);
    }

    public async ValueTask DisposeAsync()
    {
        ReleaseAnswers();
        await app.StopAsync();
        await app.DisposeAsync();
    }
}

public sealed class ShiftableTimeProvider : TimeProvider
{
    private long offsetTicks;

    public void Advance(TimeSpan by) => Interlocked.Add(ref offsetTicks, by.Ticks);

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref offsetTicks));
}
