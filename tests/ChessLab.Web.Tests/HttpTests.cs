using System.Net;
using System.Text.Json;

namespace ChessLab.Web.Tests;

[Collection(HubAppCollection.Name)]
public class HttpTests(HubApp app)
{
    private static HttpClient NewClient() => new(new HttpClientHandler { AllowAutoRedirect = false });

    [Fact]
    public async Task The_health_report_answers_anonymously_with_the_database_check()
    {
        using var http = NewClient();

        var response = await http.GetAsync($"{app.BaseUrl}/hc");
        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", report.RootElement.GetProperty("status").GetString());
        var database = report.RootElement.GetProperty("checks").GetProperty("database");
        Assert.Equal("Connected.", database.GetProperty("description").GetString());
        Assert.Equal("sqlite", database.GetProperty("data").GetProperty("provider").GetString());
        Assert.True(report.RootElement.GetProperty("uptimeSeconds").GetDouble() >= 0);
        Assert.Equal("Healthy", report.RootElement.GetProperty("checks").GetProperty("bots").GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("//example.com")]
    public async Task Logging_out_with_an_external_return_url_goes_home(string returnUrl)
    {
        using var http = NewClient();

        var response = await http.PostAsync(
            $"{app.BaseUrl}/Account/Logout?returnUrl={Uri.EscapeDataString(returnUrl)}", null);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Logging_out_with_a_local_return_url_goes_there()
    {
        using var http = NewClient();

        var response = await http.PostAsync($"{app.BaseUrl}/Account/Logout?returnUrl=/history", null);

        Assert.Equal("/history", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task A_forwarded_https_scheme_makes_the_session_cookie_secure()
    {
        using var http = NewClient();
        var login = $"{app.BaseUrl}/TestAuth/Login?userId={Guid.NewGuid()}&name=Ala";

        var plain = await http.GetAsync(login);
        using var forwarded = new HttpRequestMessage(HttpMethod.Get, login);
        forwarded.Headers.Add("X-Forwarded-Proto", "https");
        var secure = await http.SendAsync(forwarded);

        Assert.DoesNotContain("secure", string.Join(';', plain.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", string.Join(';', secure.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task The_hub_refuses_an_anonymous_caller()
    {
        using var http = NewClient();

        var response = await http.PostAsync($"{app.BaseUrl}/hubs/game/negotiate?negotiateVersion=1", null);

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.Unauthorized });
    }
}
