using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ChessLab.Core.Contracts;
using ChessLab.Core.Games;

namespace ChessLab.Web.Services;

public sealed record BotServiceSettings(Uri? ServiceUrl, string? ServiceKey, string? CertificateThumbprint)
{
    public static readonly TimeSpan RequestLimit = TimeSpan.FromSeconds(20);

    public bool IsConfigured => ServiceUrl is not null;

    public static BotServiceSettings From(IConfiguration configuration) => new(
        string.IsNullOrWhiteSpace(configuration["Bots:ServiceUrl"]) ? null : new Uri(configuration["Bots:ServiceUrl"]!),
        configuration["Bots:ServiceKey"],
        string.IsNullOrWhiteSpace(configuration["Bots:ServiceCertificateThumbprint"])
            ? null
            : configuration["Bots:ServiceCertificateThumbprint"]);

    public HttpMessageHandler CreateHandler()
    {
        var handler = new SocketsHttpHandler();
        if (CertificateThumbprint is { } thumbprint)
        {
            handler.SslOptions.RemoteCertificateValidationCallback = (_, certificate, _, errors) =>
                CertificatePin.Accepts(certificate, thumbprint);
        }

        return handler;
    }
}

public static class CertificatePin
{
    public static bool Accepts(X509Certificate? certificate, string thumbprint) =>
        certificate is not null &&
        string.Equals(
            Normalized(certificate.GetCertHashString(HashAlgorithmName.SHA256)),
            Normalized(thumbprint),
            StringComparison.OrdinalIgnoreCase);

    private static string Normalized(string thumbprint) =>
        thumbprint.Replace(":", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
}

public sealed record BotServiceProbe(bool Succeeded, long LatencyMs, string? ServiceVersion);

public sealed class BotServiceClient(HttpClient http, BotServiceSettings settings)
{
    public bool IsConfigured => settings.IsConfigured;

    public async Task<GameAction> ChooseActionAsync(BotActionRequest request, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, ServiceUri("v1/action"))
        {
            Content = JsonContent.Create(request, options: BotServiceJson.Options),
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ServiceKey);

        using var response = await http.SendAsync(message, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<GameAction>(BotServiceJson.Options, ct)
            ?? throw new InvalidOperationException("The bot service answered without an action.");
    }

    public async Task<BotServiceProbe> ProbeAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await http.GetAsync(ServiceUri("health"), ct);
            if (!response.IsSuccessStatusCode)
                return new BotServiceProbe(false, stopwatch.ElapsedMilliseconds, null);

            var health = await response.Content.ReadFromJsonAsync<BotServiceHealthDto>(BotServiceJson.Options, ct);
            return new BotServiceProbe(true, stopwatch.ElapsedMilliseconds, health?.Version);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return new BotServiceProbe(false, stopwatch.ElapsedMilliseconds, null);
        }
    }

    private Uri ServiceUri(string path) =>
        new(settings.ServiceUrl ?? throw new InvalidOperationException("No bot service is configured."), path);
}
