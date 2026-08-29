using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace DealDesk.Ops;

/// `/metrics` in the Prometheus text exposition format, hand-rolled — SPEC.md
/// feature 7 asks for the scrape target with no metrics package, so this type
/// IS the implementation: a concurrent tally map and a renderer that spells the
/// format out.
///
/// Method and response status are the only labels. The request PATH is
/// deliberately not one: `/api/appraisals/{id}` would mint a fresh time series
/// per worksheet, and unbounded label cardinality is how a hand-rolled scrape
/// target turns into memory the process never gives back. Per-request detail
/// lives in the JSONL ops ledger instead, where growth is a file on disk.
///
/// Counters only ever go up and are never reset — a restart is visible to a
/// scraper as the counter starting over, which is what a counter means.
public sealed class OpsMetrics
{
    private readonly ConcurrentDictionary<Series, Tally> tallies = new();

    /// Records one finished request. The ops middleware calls this once, after
    /// the response status is known.
    public void Observe(string method, int status, long elapsedMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        var tally = tallies.GetOrAdd(
            new Series(method.ToUpperInvariant(), status),
            _ => new Tally());

        tally.Add(elapsedMs);
    }

    /// How many requests of this method answered with this status. Zero for a
    /// series nothing has landed on — an absent series is not an error.
    public long RequestCount(string method, int status) =>
        Find(method, status)?.Requests ?? 0;

    /// Milliseconds spent on that same series, summed.
    public long DurationMs(string method, int status) =>
        Find(method, status)?.DurationMs ?? 0;

    /// The two HTTP counter families, ready to serve. Series are ordered by
    /// method then status so two scrapes of an unchanged process are
    /// byte-identical.
    public string Render()
    {
        var ordered = tallies
            .ToArray()
            .OrderBy(pair => pair.Key.Method, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Status)
            .ToList();

        var text = new StringBuilder();

        Family(
            text,
            "dealdesk_http_requests_total",
            "Requests handled since start, by method and response status.",
            "counter",
            ordered,
            tally => tally.Requests);

        Family(
            text,
            "dealdesk_http_request_duration_ms_total",
            "Milliseconds spent handling those requests, same series.",
            "counter",
            ordered,
            tally => tally.DurationMs);

        return text.ToString();
    }

    /// One labelled gauge family, rendered the same way as the counters above.
    /// The `/metrics` endpoint uses this for the worksheet counts it reads out
    /// of SQLite at scrape time, which are gauges rather than counters: they
    /// fall as well as rise.
    public static string Gauge(
        string name,
        string help,
        string labelName,
        IReadOnlyList<KeyValuePair<string, long>> series)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(labelName);
        ArgumentNullException.ThrowIfNull(series);

        var text = new StringBuilder();
        Header(text, name, help, "gauge");

        foreach (var entry in series)
        {
            text.Append(name)
                .Append('{')
                .Append(labelName)
                .Append("=\"")
                .Append(Escape(entry.Key))
                .Append("\"} ")
                .Append(entry.Value.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        return text.ToString();
    }

    /// Backslash, double quote and newline are the three characters the
    /// exposition format reserves inside a label value.
    internal static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static void Header(StringBuilder text, string name, string help, string type) =>
        text.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n')
            .Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');

    private static void Family(
        StringBuilder text,
        string name,
        string help,
        string type,
        IReadOnlyList<KeyValuePair<Series, Tally>> ordered,
        Func<Tally, long> value)
    {
        Header(text, name, help, type);

        foreach (var pair in ordered)
        {
            text.Append(name)
                .Append("{method=\"")
                .Append(Escape(pair.Key.Method))
                .Append("\",status=\"")
                .Append(pair.Key.Status.ToString(CultureInfo.InvariantCulture))
                .Append("\"} ")
                .Append(value(pair.Value).ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }
    }

    private Tally? Find(string method, int status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);

        return tallies.TryGetValue(new Series(method.ToUpperInvariant(), status), out var tally)
            ? tally
            : null;
    }

    private readonly record struct Series(string Method, int Status);

    /// Two running totals for one series. Interlocked rather than a lock: the
    /// middleware touches this on every request from every thread the server
    /// has, and a scrape must never be able to block one.
    private sealed class Tally
    {
        private long requests;

        private long durationMs;

        public long Requests => Interlocked.Read(ref requests);

        public long DurationMs => Interlocked.Read(ref durationMs);

        public void Add(long elapsedMs)
        {
            Interlocked.Increment(ref requests);
            Interlocked.Add(ref durationMs, elapsedMs);
        }
    }
}
