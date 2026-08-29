using Dapper;
using DealDesk.Data;
using Xunit;

namespace DealDesk.Tests;

public sealed class MigratorTests
{
    [Fact]
    public void Apply_runs_every_migration_then_becomes_a_no_op()
    {
        using var temp = TempDb.CreateEmpty();

        var first = temp.Db.Migrate();
        Assert.Equal(Migrator.Available().Count, first);
        Assert.NotEqual(0, first);

        // Re-running must not re-apply: migrations are append-only, so
        // "already recorded" is the whole safety story.
        Assert.Equal(0, temp.Db.Migrate());
    }

    [Fact]
    public void Applied_ids_are_recorded_in_order()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();

        var recorded = connection.Query<string>(
            "SELECT id FROM schema_migrations ORDER BY id;").ToList();

        Assert.Equal(Migrator.Available(), recorded);
    }

    [Fact]
    public void Migration_001_creates_the_appraisal_table()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();

        var name = connection.QuerySingleOrDefault<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name = 'appraisal';");

        Assert.Equal("appraisal", name);
    }
}
