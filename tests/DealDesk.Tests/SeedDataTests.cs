using System.Globalization;
using Dapper;
using Xunit;

namespace DealDesk.Tests;

/// Proves what `sql/seed.sql` actually wrote — contiguous ids, walk-item counts,
/// the comp-less worksheets, the single credit, the 24-entry trail, and recent
/// timestamps. Mirrors `AuditSchemaTests.cs` setup (TempDb, Dapper queries,
/// invented demo values) but runs against the seeded database instead of
/// constructing rows by hand.
public sealed class SeedDataTests
{
    [Fact]
    public void Appraisal_and_recon_ids_are_contiguous()
    {
        using var temp = SeedSmokeTests.SeededDb();
        using var connection = temp.Db.Open();

        var (minApp, maxApp, countApp) = connection.QuerySingle<
            (long Min, long Max, long Count)>(
            "SELECT MIN(id), MAX(id), COUNT(*) FROM appraisal;");

        Assert.Equal(1L, minApp);
        Assert.Equal(12L, maxApp);
        Assert.Equal(12L, countApp);

        var (minRl, maxRl, countRl) = connection.QuerySingle<
            (long Min, long Max, long Count)>(
            "SELECT MIN(id), MAX(id), COUNT(*) FROM recon_line;");

        Assert.Equal(1L, minRl);
        Assert.Equal(15L, maxRl);
        Assert.Equal(15L, countRl);
    }

    [Fact]
    public void Every_worksheet_carries_exactly_two_walk_items()
    {
        using var temp = SeedSmokeTests.SeededDb();
        using var connection = temp.Db.Open();

        // Every appraisal_id in walk_item must have count = 2.
        var counts = connection.Query<int>(
                "SELECT COUNT(*) FROM walk_item GROUP BY appraisal_id")
            .ToList();

        Assert.Equal(12, counts.Count); // twelve distinct worksheets

        foreach (var c in counts)
        {
            Assert.Equal(2, c);
        }
    }

    [Fact]
    public void Two_worksheets_have_no_comps()
    {
        using var temp = SeedSmokeTests.SeededDb();
        using var connection = temp.Db.Open();

        // Worksheet 12 has no comps (the car that just landed).
        var w12Count = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM comp WHERE appraisal_id = 12;");
        Assert.Equal(0, w12Count);

        // Worksheet 8 has no comps (lost before pricing).
        var w8Count = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM comp WHERE appraisal_id = 8;");
        Assert.Equal(0, w8Count);

        // Ten distinct worksheets carry comps (29 total).
        var distinctCount = connection.ExecuteScalar<int>(
            "SELECT COUNT(DISTINCT appraisal_id) FROM comp;");
        Assert.Equal(10, distinctCount);
    }

    [Fact]
    public void Nine_lines_posted_and_one_credit_exists()
    {
        using var temp = SeedSmokeTests.SeededDb();
        using var connection = temp.Db.Open();

        // Nine of the fifteen recon lines have at least one posting;
        // six are still unposted.
        var postedLineCount = connection.ExecuteScalar<int>(
            """
            SELECT COUNT(DISTINCT ra.recon_line_id)
            FROM recon_actual ra
            """);
        Assert.Equal(9, postedLineCount);

        // Exactly one row carries a negative amount (the credit).
        var negativeCount = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM recon_actual WHERE amount < 0;");
        Assert.Equal(1, negativeCount);

        // Its value is -15_000.
        var negativeAmount = connection.ExecuteScalar<long>(
            "SELECT amount FROM recon_actual WHERE amount < 0;");
        Assert.Equal(-15_000L, negativeAmount);
    }

    [Fact]
    public void The_trail_is_24_entries_with_status_dominance()
    {
        using var temp = SeedSmokeTests.SeededDb();
        using var connection = temp.Db.Open();

        var totalTrail = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM audit_entry;");
        Assert.Equal(24, totalTrail);

        var statusEntries = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM audit_entry WHERE field = 'status';");
        Assert.Equal(21, statusEntries);

        // Worksheet 3 has status entries; the latest new_value is 'lost'
        // (it went draft → appraised → lost).
        var w3Latest = connection.QueryFirstOrDefault<string>(
            """
            SELECT new_value FROM audit_entry
            WHERE appraisal_id = 3 AND field = 'status'
            ORDER BY id DESC LIMIT 1
            """);
        Assert.Equal("lost", w3Latest);

        // Worksheet 3 also has an 'appraised' entry — the intermediate step.
        var w3Appraised = connection.QueryFirstOrDefault<string>(
            """
            SELECT new_value FROM audit_entry
            WHERE appraisal_id = 3 AND field = 'status' AND new_value = 'appraised'
            """);
        Assert.Equal("appraised", w3Appraised);

        // Worksheet 8 has exactly one trail entry — the draft→lost move.
        var w8Count = connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM audit_entry WHERE appraisal_id = 8;");
        Assert.Equal(1, w8Count);

        // That single entry is the draft→lost move.
        var w8Value = connection.QueryFirstOrDefault<string>(
            """
            SELECT new_value FROM audit_entry
            WHERE appraisal_id = 8
            ORDER BY id DESC LIMIT 1
            """);
        Assert.Equal("lost", w8Value);
    }

    [Fact]
    public void Every_created_at_is_recent_and_not_in_the_future()
    {
        using var temp = SeedSmokeTests.SeededDb();
        using var connection = temp.Db.Open();

        var now = DateTimeOffset.UtcNow;
        var timestamps = connection.Query<string>(
            "SELECT created_at FROM appraisal;");

        foreach (var value in timestamps)
        {
            var parsed = DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

            // Within the last 31 days.
            Assert.True(parsed >= now.AddDays(-31),
                value + " is older than 31 days");

            // Not in the future.
            Assert.True(parsed <= now,
                value + " is in the future");
        }
    }
}
