using DealDesk.Data;

namespace DealDesk.Tests;

/// A migrated SQLite database in a fresh temp directory, deleted on Dispose.
/// Every database-touching test owns one — tests never share a file, so they
/// run in any order and in parallel.
internal sealed class TempDb : IDisposable
{
    private readonly string directory;

    private TempDb(string directory, Db db)
    {
        this.directory = directory;
        Db = db;
    }

    public Db Db { get; }

    /// Creates the temp file and applies every migration.
    public static TempDb CreateMigrated()
    {
        var db = CreateEmptyCore(out var directory);
        db.Migrate();
        return new TempDb(directory, db);
    }

    /// Creates the temp file with NO migrations applied — for tests that
    /// exercise the migrator itself.
    public static TempDb CreateEmpty()
    {
        var db = CreateEmptyCore(out var directory);
        return new TempDb(directory, db);
    }

    private static Db CreateEmptyCore(out string directory)
    {
        directory = Path.Combine(Path.GetTempPath(), "dealdesk-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return new Db(Path.Combine(directory, "test.db"));
    }

    public void Dispose()
    {
        // Sqlite keeps the file handle in a connection pool; release it so the
        // temp directory can actually go away on Windows and Linux alike.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp file is not a test failure.
        }
    }
}
