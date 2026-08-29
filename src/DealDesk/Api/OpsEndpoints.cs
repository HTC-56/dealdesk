using Dapper;
using DealDesk.Data;
using DealDesk.Domain;
using DealDesk.Ops;

namespace DealDesk.Api;

/// `GET /metrics` — the scrape target of SPEC.md feature 7, beside the
/// `/healthz` that has been in Program.cs since Phase A.
///
/// Two families, and they answer different questions. The HTTP counters come
/// out of memory and say how the service is behaving; the worksheet gauges are
/// read from SQLite at scrape time and say what the desk is holding. A
/// director watching a month of appraisals and an operator watching a process
/// read the same page.
///
/// Like `/healthz`, this route is open: a scraper is not a caller with a
/// worksheet to change, and metrics carry no vehicle, no customer and no money
/// — only counts.
public static class OpsEndpoints
{
    /// The exposition format's own content type, version and all. Prometheus
    /// accepts `text/plain` without it; naming the version is what tells a
    /// reader which format this hand-rolled text claims to be.
    internal const string PrometheusContentType = "text/plain; version=0.0.4; charset=utf-8";

    public static void MapOpsEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet("/metrics", (Db db, OpsMetrics metrics) =>
        {
            using var connection = db.Open();

            var counted = connection.Query<StatusCount>(
                "SELECT status, COUNT(*) AS count FROM appraisal GROUP BY status;")
                .ToDictionary(row => row.Status, row => row.Count, StringComparer.Ordinal);

            // Every lifecycle status is emitted, including the ones at zero: a
            // series that vanishes when its count empties reads on a dashboard
            // as a scrape that failed rather than a store that sold nothing.
            var series = Lifecycle.All
                .Select(status => new KeyValuePair<string, long>(
                    status,
                    counted.TryGetValue(status, out var count) ? count : 0))
                .ToList();

            var text = metrics.Render() + OpsMetrics.Gauge(
                "dealdesk_appraisals",
                "Worksheets in the database, by lifecycle status.",
                "status",
                series);

            return Results.Text(text, PrometheusContentType);
        });
    }
}

/// One `GROUP BY status` row. `COUNT(*)` is the one computed column in the
/// repo that needs an alias — every other query selects real column names and
/// lets Dapper's underscore matching do the mapping.
internal sealed record StatusCount
{
    public string Status { get; init; } = string.Empty;

    public long Count { get; init; }
}
