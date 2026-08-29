using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// The ops surface of SPEC.md feature 7, end to end: what a scraper reads,
/// what an operator tails, and what a caller without a token gets.
///
/// The three pieces are one pipeline. A request is timed by the recording
/// middleware, counted into `/metrics`, written as a line of the JSONL ledger,
/// and — when a token is configured — refused before it reaches a handler. The
/// later ops test files mirror this one.
public sealed class OpsSmokeTests
{
    /// The invented token the ops tests arm the guard with. No real credential
    /// appears anywhere in this repo.
    internal const string DemoToken = "desk-demo-token";

    /// Scraping tells an operator how the service is behaving: two counter
    /// families, labelled by method and status, in the exposition format
    /// Prometheus reads.
    [Fact]
    public async Task Metrics_serves_prometheus_text_counting_the_requests_it_answered()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using (var health = await client.GetAsync(new Uri("/healthz", UriKind.Relative)))
        {
            health.EnsureSuccessStatusCode();
        }

        var text = await ScrapeUntilAsync(
            client,
            scrape => scrape.Contains(
                "dealdesk_http_requests_total{method=\"GET\",status=\"200\"}",
                StringComparison.Ordinal));

        Assert.Contains("# TYPE dealdesk_http_requests_total counter", text, StringComparison.Ordinal);
        Assert.Contains(
            "# TYPE dealdesk_http_request_duration_ms_total counter",
            text,
            StringComparison.Ordinal);

        // Every line of a family sits under its own HELP and TYPE, and the
        // whole document ends in a newline — both are format, not decoration.
        Assert.Contains("# HELP dealdesk_appraisals ", text, StringComparison.Ordinal);
        Assert.EndsWith("\n", text, StringComparison.Ordinal);
    }

    /// The gauge half of the scrape says what the desk is holding rather than
    /// how the process is behaving, and it names every lifecycle status even
    /// when the count is zero.
    [Fact]
    public async Task Metrics_gauges_every_lifecycle_status_and_counts_a_new_worksheet()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var text = await ScrapeUntilAsync(client, _ => true);

        Assert.Contains("# TYPE dealdesk_appraisals gauge", text, StringComparison.Ordinal);
        foreach (var status in new[] { "draft", "appraised", "presented", "won", "lost" })
        {
            Assert.Contains(
                $"dealdesk_appraisals{{status=\"{status}\"}} 0",
                text,
                StringComparison.Ordinal);
        }

        await ReportSmokeTests.CreateAsync(client, "A. Whitfield");

        var second = await ScrapeUntilAsync(
            client,
            scrape => scrape.Contains(
                "dealdesk_appraisals{status=\"draft\"} 1",
                StringComparison.Ordinal));

        Assert.Contains("dealdesk_appraisals{status=\"lost\"} 0", second, StringComparison.Ordinal);
    }

    /// The ledger is the other half: one JSON object per request, appended,
    /// carrying the path the metric labels deliberately leave out.
    [Fact]
    public async Task Ops_ledger_appends_one_json_line_per_request()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using (var health = await client.GetAsync(new Uri("/healthz", UriKind.Relative)))
        {
            health.EnsureSuccessStatusCode();
        }

        using (var missing = await client.GetAsync(new Uri("/api/appraisals/9999", UriKind.Relative)))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        var lines = await LedgerLinesAsync(factory, 2);
        Assert.Equal(2, lines.Count);

        using var first = JsonDocument.Parse(lines[0]);
        Assert.Equal("GET", first.RootElement.GetProperty("method").GetString());
        Assert.Equal("/healthz", first.RootElement.GetProperty("path").GetString());
        Assert.Equal(200, first.RootElement.GetProperty("status").GetInt32());
        Assert.True(first.RootElement.GetProperty("ms").GetInt64() >= 0);
        Assert.NotNull(first.RootElement.GetProperty("ts").GetString());

        // A refusal is exactly the line an operator wants, so it is written
        // like any other answer.
        using var second = JsonDocument.Parse(lines[1]);
        Assert.Equal("/api/appraisals/9999", second.RootElement.GetProperty("path").GetString());
        Assert.Equal(404, second.RootElement.GetProperty("status").GetInt32());
    }

    /// With a token configured, writes need it and reads do not.
    [Fact]
    public async Task Bearer_token_guards_writes_and_leaves_reads_open()
    {
        using var factory = new DeskAppFactory(DemoToken);
        using var client = factory.CreateClient();

        using (var refused = await client.PostAsJsonAsync("/api/appraisals", NewWorksheet()))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
            Assert.Contains(
                refused.Headers.WwwAuthenticate,
                header => string.Equals(header.Scheme, "Bearer", StringComparison.Ordinal));
        }

        using (var open = await client.GetAsync(new Uri("/api/appraisals", UriKind.Relative)))
        {
            open.EnsureSuccessStatusCode();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", DemoToken);

        using var created = await client.PostAsJsonAsync("/api/appraisals", NewWorksheet());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
    }

    /// A worksheet body good enough for POST /api/appraisals — the same
    /// invented vehicle the report tests use.
    internal static object NewWorksheet() => new
    {
        vin = "ZZ9ZZ99Z2Z9000042",
        modelYear = 2019,
        make = "Meridian",
        model = "Trailhead",
        trimLevel = "LT",
        miles = 74_311,
        appraiser = "A. Whitfield",
    };

    /// Scrapes `/metrics` until the predicate holds, or gives up and returns
    /// the last scrape so the assertion that follows reports the real text.
    ///
    /// Both observers record AFTER the response has gone back to the caller —
    /// that is the point of them, a request is never delayed to be written
    /// down — so a test that reads immediately can read one request early.
    internal static async Task<string> ScrapeUntilAsync(HttpClient client, Func<string, bool> until)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(until);

        var text = string.Empty;

        for (var attempt = 0; attempt < 50; attempt++)
        {
            using var response = await client.GetAsync(new Uri("/metrics", UriKind.Relative));
            response.EnsureSuccessStatusCode();
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);

            text = await response.Content.ReadAsStringAsync();
            if (until(text))
            {
                return text;
            }

            await Task.Delay(20);
        }

        return text;
    }

    /// Reads the factory's ledger file once it holds at least `atLeast` lines.
    internal static async Task<IReadOnlyList<string>> LedgerLinesAsync(
        DeskAppFactory factory, int atLeast)
    {
        ArgumentNullException.ThrowIfNull(factory);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (File.Exists(factory.LedgerPath))
            {
                var lines = await File.ReadAllLinesAsync(factory.LedgerPath);
                if (lines.Length >= atLeast)
                {
                    return lines;
                }
            }

            await Task.Delay(20);
        }

        return File.Exists(factory.LedgerPath)
            ? await File.ReadAllLinesAsync(factory.LedgerPath)
            : [];
    }
}
