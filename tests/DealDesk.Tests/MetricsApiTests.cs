using System.Net.Http.Json;
using Xunit;

namespace DealDesk.Tests;

/// Six HTTP assertions over GET /metrics — what a scraper reads, the counts
/// it sees, and the fact that no token is required.
///
/// Each test gets its own factory, so no test sees another's counts. Uses
/// OpsSmokeTests.ScrapeUntilAsync with predicates to wait for expected series.
public sealed class MetricsApiTests
{
    /// GET /metrics answers 200 with the Prometheus content type and version.
    [Fact]
    public async Task Metrics_answers_200_with_text_plain_version_0_0_4()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("version=0.0.4", response.Content.Headers.ContentType?.ToString() ?? "");
    }

    /// A miss is counted under its own status: a 404 shows up as a 404 line.
    [Fact]
    public async Task A_miss_is_counted_under_its_own_status()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var _ = await client.GetAsync(new Uri("/api/appraisals/9999", UriKind.Relative));

        var text = await OpsSmokeTests.ScrapeUntilAsync(
            client,
            scrape => scrape.Contains(
                "dealdesk_http_requests_total{method=\"GET\",status=\"404\"} 1",
                StringComparison.Ordinal));

        Assert.Contains(
            "dealdesk_http_requests_total{method=\"GET\",status=\"404\"} 1",
            text,
            StringComparison.Ordinal);
    }

    /// A write is counted under its own method: POST to create returns 201.
    [Fact]
    public async Task A_write_is_counted_under_its_own_method()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        await ReportSmokeTests.CreateAsync(client, "A. Whitfield");

        var text = await OpsSmokeTests.ScrapeUntilAsync(
            client,
            scrape => scrape.Contains(
                "dealdesk_http_requests_total{method=\"POST\",status=\"201\"} 1",
                StringComparison.Ordinal));

        Assert.Contains(
            "dealdesk_http_requests_total{method=\"POST\",status=\"201\"} 1",
            text,
            StringComparison.Ordinal);
    }

    /// The gauge falls as well as rises: create one worksheet and walk it to
    /// won, then draft drops to zero and won rises to one.
    [Fact]
    public async Task The_gauge_falls_as_well_as_rises()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(client, id, "appraised", "presented", "won");

        var text = await OpsSmokeTests.ScrapeUntilAsync(
            client,
            scrape => scrape.Contains("dealdesk_appraisals{status=\"won\"} 1", StringComparison.Ordinal));

        Assert.Contains("dealdesk_appraisals{status=\"draft\"} 0", text, StringComparison.Ordinal);
        Assert.Contains("dealdesk_appraisals{status=\"won\"} 1", text, StringComparison.Ordinal);
    }

    /// The duration family carries the same series as the count family.
    [Fact]
    public async Task The_duration_family_carries_the_same_series_as_the_count_family()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var _ = await client.GetAsync(new Uri("/healthz", UriKind.Relative));

        var text = await OpsSmokeTests.ScrapeUntilAsync(
            client,
            scrape => scrape.Contains(
                "dealdesk_http_request_duration_ms_total{method=\"GET\",status=\"200\"}",
                StringComparison.Ordinal));

        Assert.Contains(
            "dealdesk_http_request_duration_ms_total{method=\"GET\",status=\"200\"}",
            text,
            StringComparison.Ordinal);
    }

    /// A scraper needs no token: even with a token configured, /metrics is
    /// still accessible without one.
    [Fact]
    public async Task No_token_is_needed_for_metrics()
    {
        using var factory = new DeskAppFactory(OpsSmokeTests.DemoToken);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }
}
