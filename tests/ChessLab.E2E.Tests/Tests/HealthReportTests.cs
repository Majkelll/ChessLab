using System.Net;
using System.Text.Json;

namespace ChessLab.E2E.Tests.Tests;

[Collection(AppCollection.Name)]
public sealed class HealthReportTests(WebAppFixture app)
{
    [Fact]
    public async Task Health_report_is_public_and_says_the_database_is_reachable()
    {
        using var http = new HttpClient();

        var response = await http.GetAsync($"{app.BaseUrl}/hc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var report = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", report.RootElement.GetProperty("status").GetString());
        Assert.Equal("Healthy", report.RootElement.GetProperty("checks").GetProperty("database").GetProperty("status").GetString());
    }
}
