using DealDesk.Ops;
using Xunit;

namespace DealDesk.Tests;

/// Metric arithmetic laws — empty family, separate totals, case normalisation,
/// status series, render ordering, Gauge + Escape. Each law is one [Fact],
/// plain Assert calls, no database and no HTTP.
public sealed class OpsMetricsTests
{
    [Fact]
    public void A_fresh_OpsMetrics_reports_zero_and_declares_itself()
    {
        var metrics = new OpsMetrics();

        Assert.Equal(0, metrics.RequestCount("GET", 200));
        Assert.Equal(0, metrics.DurationMs("GET", 200));

        var rendered = metrics.Render();
        Assert.Contains("# TYPE dealdesk_http_requests_total counter", rendered);
    }

    [Fact]
    public void Two_Observe_calls_accumulate_count_and_ms_separately()
    {
        var metrics = new OpsMetrics();

        metrics.Observe("GET", 200, 5);
        metrics.Observe("GET", 200, 5);

        Assert.Equal(2, metrics.RequestCount("GET", 200));
        Assert.Equal(10, metrics.DurationMs("GET", 200));
    }

    [Fact]
    public void Method_is_uppercased_before_counting()
    {
        var metrics = new OpsMetrics();

        metrics.Observe("get", 200, 1);
        metrics.Observe("GET", 200, 1);

        Assert.Equal(2, metrics.RequestCount("GET", 200));
    }

    [Fact]
    public void Status_separates_series()
    {
        var metrics = new OpsMetrics();

        metrics.Observe("GET", 200, 1);
        metrics.Observe("GET", 404, 1);

        Assert.Equal(1, metrics.RequestCount("GET", 200));

        var rendered = metrics.Render();
        Assert.Contains("status=\"200\"", rendered);
        Assert.Contains("status=\"404\"", rendered);
    }

    [Fact]
    public void Render_is_ordered_by_method_then_status()
    {
        var metrics = new OpsMetrics();

        // Observe POST first, GET second — order must NOT matter in output
        metrics.Observe("POST", 200, 1);
        metrics.Observe("GET", 200, 1);

        var rendered = metrics.Render();

        var getIndex = rendered.IndexOf("method=\"GET\"", StringComparison.Ordinal);
        var postIndex = rendered.IndexOf("method=\"POST\"", StringComparison.Ordinal);

        Assert.True(getIndex < postIndex,
            $"method=\"GET\" (at {getIndex}) should come before method=\"POST\" (at {postIndex})");
    }

    [Fact]
    public void Gauge_and_Escape_render_correctly()
    {
        var series = new[]
        {
            new KeyValuePair<string, long>("draft", 2L)
        };

        var gauge = OpsMetrics.Gauge(
            "dealdesk_test",
            "help text",
            "status",
            series);

        Assert.Contains("# TYPE dealdesk_test gauge", gauge);
        Assert.Contains("dealdesk_test{status=\"draft\"} 2", gauge);

        Assert.Equal("a\\\"b", OpsMetrics.Escape("a\"b"));
    }
}
