using Dapper;
using Microsoft.Data.Sqlite;

namespace DealDesk.Data;

/// Opens SQLite connections to one database file. Dapper does the mapping;
/// there is no ORM here on purpose — the SQL is part of the work sample.
public sealed class Db
{
    private static int typeHandlersRegistered;

    private readonly string connectionString;

    public Db(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Path = path;
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        RegisterTypeHandlers();
    }

    public string Path { get; }

    /// Opens a connection with the pragmas dealdesk relies on: WAL for
    /// concurrent readers, and foreign keys actually enforced (SQLite leaves
    /// them off by default, per-connection).
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        connection.Execute("PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;");
        return connection;
    }

    /// Brings the database up to the newest migration. Returns how many ran.
    public int Migrate()
    {
        using var connection = Open();
        return Migrator.Apply(connection);
    }

    /// Idempotent — safe to call from every Db and from test fixtures.
    public static void RegisterTypeHandlers()
    {
        if (Interlocked.Exchange(ref typeHandlersRegistered, 1) == 1)
        {
            return;
        }

        // The schema is snake_case and the C# is PascalCase; letting Dapper
        // bridge that means queries select real column names instead of
        // carrying a column alias on every line.
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        SqlMapper.AddTypeHandler(new MoneyTypeHandler());
    }
}
