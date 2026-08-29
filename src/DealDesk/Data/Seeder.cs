using Dapper;
using Microsoft.Data.Sqlite;

namespace DealDesk.Data;

/// Writes the demo month of sql/seed.sql — SPEC.md feature 8 — so the desk
/// page and the three reports have something to show on a fresh clone.
///
/// The script is embedded beside the migrations but is NOT one: Migrator only
/// recognises `NNN_name.sql`, so nothing applies the seed automatically. That
/// separation is the point. A migration must run on every database, including
/// a real one; demo data must run on none of them unless somebody asks.
///
/// Seeding is REFUSED, not repeated, when the appraisal table already holds
/// rows. Running it twice would write a second demo month over the first, and
/// every report would then read as a store that did twice the business it did.
/// A refusal returns 0 rather than throwing — "already seeded" is the expected
/// state of a database somebody has already demoed, not an error.
public static class Seeder
{
    private const string Resource = "sql.seed.sql";

    /// Writes the demo month, unless the database already holds worksheets.
    /// Returns how many appraisals it wrote — 0 when it declined.
    public static int Apply(Db db)
    {
        ArgumentNullException.ThrowIfNull(db);

        using var connection = db.Open();
        return Apply(connection);
    }

    /// Same, against a connection the caller already owns.
    public static int Apply(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        if (connection.ExecuteScalar<long>("SELECT COUNT(*) FROM appraisal;") > 0)
        {
            return 0;
        }

        // One transaction for the whole month: a half-written demo month would
        // be worse than none, because its reports would still answer 200.
        using var tx = connection.BeginTransaction();
        connection.Execute(Script(), transaction: tx);
        var written = connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM appraisal;", transaction: tx);
        tx.Commit();

        return (int)written;
    }

    /// The seed script as embedded in this assembly, so a single-file publish
    /// carries its own demo data exactly as it carries its own schema.
    public static string Script()
    {
        using var stream = typeof(Seeder).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"Seed resource '{Resource}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
