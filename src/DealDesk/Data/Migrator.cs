using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace DealDesk.Data;

/// Applies the numbered scripts in sql/ (embedded at build time) to a SQLite
/// database, exactly once each, in filename order. Migrations are append-only:
/// a committed script is never edited, so "already applied" is always safe.
public static partial class Migrator
{
    private const string ResourcePrefix = "sql.";

    [GeneratedRegex(@"^sql\.(?<id>\d{3}_[A-Za-z0-9_]+)\.sql$")]
    private static partial Regex MigrationName();

    /// Returns the migration ids carried by this assembly, in apply order.
    public static IReadOnlyList<string> Available()
    {
        var found = new List<string>();
        foreach (var resource in typeof(Migrator).Assembly.GetManifestResourceNames())
        {
            var match = MigrationName().Match(resource);
            if (match.Success)
            {
                found.Add(match.Groups["id"].Value);
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// Applies every migration not yet recorded in schema_migrations.
    /// Returns how many were applied this call; a second call returns 0.
    public static int Apply(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Execute(connection, """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                id         TEXT PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """);

        var done = AlreadyApplied(connection);
        var applied = 0;

        foreach (var id in Available())
        {
            if (done.Contains(id))
            {
                continue;
            }

            var body = ReadScript(id);

            // One transaction per script: a failing migration leaves no partial
            // schema and no schema_migrations row behind.
            using var tx = connection.BeginTransaction();
            Execute(connection, body, tx);

            using (var record = connection.CreateCommand())
            {
                record.Transaction = tx;
                record.CommandText =
                    "INSERT INTO schema_migrations (id, applied_at) VALUES ($id, $at);";
                record.Parameters.AddWithValue("$id", id);
                record.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O"));
                record.ExecuteNonQuery();
            }

            tx.Commit();
            applied++;
        }

        return applied;
    }

    private static HashSet<string> AlreadyApplied(SqliteConnection connection)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM schema_migrations;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static string ReadScript(string id)
    {
        var resource = ResourcePrefix + id + ".sql";
        using var stream = typeof(Migrator).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Migration resource '{resource}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void Execute(SqliteConnection connection, string sql, SqliteTransaction? tx = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
