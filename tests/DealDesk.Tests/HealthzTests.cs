using System.Net;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// The in-process integration pattern: new DeskAppFactory, CreateClient, assert
/// on the real HTTP response. Later endpoint tests mirror this file.
public sealed class HealthzTests
{
    [Fact]
    public async Task Healthz_reports_ok_and_the_applied_schema_version()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/healthz", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());

        // Startup migrated the temp database, so healthz must name a real
        // migration rather than "none".
        var schema = json.RootElement.GetProperty("schema").GetString();
        Assert.NotNull(schema);
        Assert.NotEqual("none", schema);
        Assert.StartsWith("001_", schema, StringComparison.Ordinal);
    }
}
