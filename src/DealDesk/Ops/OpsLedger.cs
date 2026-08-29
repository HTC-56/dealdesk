using System.Globalization;
using System.Text.Json;

namespace DealDesk.Ops;

/// The JSONL ops ledger SPEC.md feature 7 names: one line of JSON per request,
/// appended and never rewritten, so `tail -f` is a live view of the desk and
/// `grep` is the whole query language.
///
/// It is the counterpart to `/metrics`. Metrics keep bounded aggregates in
/// memory and forget which worksheet was touched; the ledger keeps the path
/// with every line and forgets nothing, at the price of growing on disk. That
/// split is why the metric labels carry no path.
///
/// A ledger is an observer, not a participant: a request that cannot be
/// written down still succeeds. Every write failure is swallowed rather than
/// surfaced, because losing an ops line is not worth failing an appraisal that
/// the database has already committed.
public sealed class OpsLedger
{
    private readonly object gate = new();

    /// Null when no path was configured, which turns the ledger off entirely.
    private readonly string? path;

    public OpsLedger(string? path) =>
        this.path = string.IsNullOrWhiteSpace(path) ? null : path.Trim();

    /// False when the ledger has no path and is writing nothing.
    public bool Enabled => path is not null;

    /// Where lines land, or null when the ledger is off.
    public string? Path => path;

    /// One request, as it will appear on disk. Pure and timestamp-injected so
    /// it can be asserted without a clock: the caller decides `at`.
    ///
    /// Field order is the order a reader scans — when, what, how it went — and
    /// the payload is serialised rather than concatenated so a path carrying a
    /// quote cannot break the line it is written on.
    internal static string Line(
        string method,
        string requestPath,
        int status,
        long elapsedMs,
        DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentNullException.ThrowIfNull(requestPath);

        return JsonSerializer.Serialize(new
        {
            ts = at.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            method,
            path = requestPath,
            status,
            ms = elapsedMs,
        });
    }

    /// Appends one line for a finished request. Does nothing when the ledger is
    /// off. Serialised on a lock: many request threads share one file, and a
    /// half-written line would cost a reader the whole record.
    public void Append(string method, string requestPath, int status, long elapsedMs) =>
        Append(method, requestPath, status, elapsedMs, DateTimeOffset.UtcNow);

    /// The clock-injected form, for tests that assert on the timestamp.
    public void Append(
        string method,
        string requestPath,
        int status,
        long elapsedMs,
        DateTimeOffset at)
    {
        if (path is null)
        {
            return;
        }

        var line = Line(method, requestPath, status, elapsedMs, at) + "\n";

        try
        {
            lock (gate)
            {
                var directory = System.IO.Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(path, line);
            }
        }
        catch (IOException)
        {
            // A full disk or a locked file loses an ops line, not a request.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: an unwritable ledger path is a deployment problem to read
            // in the logs, never a 500 handed to the desk.
        }
    }
}
