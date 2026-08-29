using System.Text.Json;
using DealDesk.Ops;
using Xunit;

namespace DealDesk.Tests;

/// OpsLedger file behaviour — six laws over the JSONL writer.
/// Each law is one [Fact], no database and no HTTP.
public sealed class OpsLedgerTests
{
    private static readonly DateTimeOffset At =
        new(2026, 8, 29, 12, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Null_or_whitespace_path_turns_ledger_off()
    {
        var ledger = new OpsLedger(null!);
        Assert.False(ledger.Enabled);
        Assert.Null(ledger.Path);

        var blank = new OpsLedger("   ");
        Assert.False(blank.Enabled);
        Assert.Null(blank.Path);

        // Appending to a disabled ledger must not throw or create a file.
        ledger.Append("GET", "/healthz", 200, 3);
        blank.Append("POST", "/api/x", 400, 1);
    }

    [Fact]
    public void Append_appends_not_rewrites()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dealdesk-ledger-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "ledger.jsonl");

            var ledger = new OpsLedger(path);
            ledger.Append("GET", "/healthz", 200, 3, At);
            // #pragma warning disable xUnit2013 // "Use Assert.Single" — we assert size 1 here
            Assert.Single(File.ReadAllLines(path));
            // #pragma warning restore xUnit2013

            var first = File.ReadAllLines(path)[0];
            ledger.Append("POST", "/api/x", 201, 7, At);
            var lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.Equal(first, lines[0]);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }

    [Fact]
    public void Line_parses_as_JSON_with_correct_fields()
    {
        var json = OpsLedger.Line("GET", "/healthz", 200, 3, At);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("GET", root.GetProperty("method").GetString());
        Assert.Equal("/healthz", root.GetProperty("path").GetString());
        Assert.Equal(200, root.GetProperty("status").GetInt32());
        Assert.Equal(3, root.GetProperty("ms").GetInt32());
    }

    [Fact]
    public void Line_records_ts_in_utc()
    {
        var json = OpsLedger.Line("GET", "/healthz", 200, 3, At);
        using var doc = JsonDocument.Parse(json);
        var ts = doc.RootElement.GetProperty("ts").GetString();

        Assert.StartsWith("2026-08-29T10:00:00", ts);
    }

    [Fact]
    public void Line_escapes_quotes_in_path()
    {
        var json = OpsLedger.Line("GET", "/api/x\"y", 200, 1, At);
        using var doc = JsonDocument.Parse(json);
        var path = doc.RootElement.GetProperty("path").GetString();

        Assert.Equal("/api/x\"y", path);
    }

    [Fact]
    public void Append_creates_missing_parent_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dealdesk-ledger-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(dir, "nested", "ledger.jsonl");
            var ledger = new OpsLedger(path);

            ledger.Append("GET", "/a", 200, 1, At);
            ledger.Append("GET", "/b", 200, 2, At);
            ledger.Append("GET", "/c", 200, 3, At);

            var lines = File.ReadAllLines(path);
            Assert.Equal(3, lines.Length);
            foreach (var line in lines)
            {
                using var doc = JsonDocument.Parse(line);
                Assert.True(doc.RootElement.ValueKind == JsonValueKind.Object);
            }
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { }
        }
    }
}
