using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DealDesk.Tests;

/// Phase B worksheet schema constraint tests — prove that `sql/002_worksheet.sql`'s
/// CHECK constraints reject bad data and that cascade delete works. Mirrors
/// `AppraisalConstraintTests.cs` setup (TempDb, parameterised INSERT, invented demo
/// values) but asserts that violating rows throw `SqliteException` instead of succeeding.
public sealed class WorksheetSchemaTests
{
    // Invented vehicle: no real VIN, no real make or model, no real person.
    private const string Vin = "ZZ9ZZ99Z2Z9000042";

    /// Insert one appraisal row and return its id via RETURNING id.
    private static long CreateAppraisal(Microsoft.Data.Sqlite.SqliteConnection conn)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        return conn.QuerySingle<long>(
            """
            INSERT INTO appraisal
                (vin, model_year, make, model, trim_level, miles, appraiser,
                 status, created_at, updated_at)
            VALUES
                ($vin, $year, $make, $model, $trim, $miles, $appraiser,
                 $status, $created_at, $updated_at)
            RETURNING id
            """,
            new
            {
                vin = Vin,
                year = 2019,
                make = "Meridian",
                model = "Trailhead",
                trim = "SE",
                miles = 74_311,
                appraiser = "A. Whitfield",
                status = "draft",
                created_at = now,
                updated_at = now,
            });
    }

    [Fact]
    public void Recon_line_category_outside_vocabulary_is_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO recon_line
                    (appraisal_id, category, description, estimate, created_at)
                VALUES
                    ($appraisal_id, $category, $description, $estimate, $created_at)
                """,
                new
                {
                    appraisal_id = id,
                    category = "wheels",
                    description = "Set of four tyres",
                    estimate = 40_000,
                    created_at = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Recon_line_valid_category_inserts()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        // No exception means the constraint accepted the row.
        connection.Execute(
            """
            INSERT INTO recon_line
                (appraisal_id, category, description, estimate, created_at)
            VALUES
                ($appraisal_id, $category, $description, $estimate, $created_at)
            """,
            new
            {
                appraisal_id = id,
                category = "mechanical",
                description = "New clutch assembly",
                estimate = 2_500_00,
                created_at = now,
            });
    }

    [Fact]
    public void Recon_line_negative_estimate_is_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO recon_line
                    (appraisal_id, category, description, estimate, created_at)
                VALUES
                    ($appraisal_id, $category, $description, $estimate, $created_at)
                """,
                new
                {
                    appraisal_id = id,
                    category = "body",
                    description = "Dent repair",
                    estimate = -500,
                    created_at = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Recon_line_empty_description_is_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO recon_line
                    (appraisal_id, category, description, estimate, created_at)
                VALUES
                    ($appraisal_id, $category, $description, $estimate, $created_at)
                """,
                new
                {
                    appraisal_id = id,
                    category = "body",
                    description = "",
                    estimate = 1_000,
                    created_at = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Comp_with_zero_price_is_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO comp
                    (appraisal_id, label, model_year, miles, price, note, created_at)
                VALUES
                    ($appraisal_id, $label, $year, $miles, $price, $note, $created_at)
                """,
                new
                {
                    appraisal_id = id,
                    label = "Similar unit",
                    year = 2020,
                    miles = 50_000,
                    price = 0,
                    note = "",
                    created_at = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Comp_with_positive_price_inserts()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        // No exception means the constraint accepted the row.
        connection.Execute(
            """
            INSERT INTO comp
                (appraisal_id, label, model_year, miles, price, note, created_at)
            VALUES
                ($appraisal_id, $label, $year, $miles, $price, $note, $created_at)
            """,
            new
            {
                appraisal_id = id,
                label = "Similar unit",
                year = 2020,
                miles = 50_000,
                price = 18_500_00,
                note = "",
                created_at = now,
            });
    }

    [Fact]
    public void Walk_item_severity_outside_vocabulary_is_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO walk_item
                    (appraisal_id, area, severity, note, created_at)
                VALUES
                    ($appraisal_id, $area, $severity, $note, $created_at)
                """,
                new
                {
                    appraisal_id = id,
                    area = "exterior",
                    severity = "catastrophic",
                    note = "Total loss",
                    created_at = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Walk_item_valid_severity_inserts()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        // No exception means the constraint accepted the row.
        connection.Execute(
            """
            INSERT INTO walk_item
                (appraisal_id, area, severity, note, created_at)
            VALUES
                ($appraisal_id, $area, $severity, $note, $created_at)
            """,
            new
            {
                appraisal_id = id,
                area = "interior",
                severity = "moderate",
                note = "Worn seat cover",
                created_at = now,
            });
    }

    [Fact]
    public void Offer_input_negative_pack_is_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO offer_input
                    (appraisal_id, pack, target_gross, updated_at)
                VALUES
                    ($appraisal_id, $pack, $target_gross, $updated_at)
                """,
                new
                {
                    appraisal_id = id,
                    pack = -1,
                    target_gross = 3_000_00,
                    updated_at = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Cascade_delete_removes_recon_line_children()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        // Insert three recon_line children individually.
        connection.Execute(
            """
            INSERT INTO recon_line
                (appraisal_id, category, description, estimate, created_at)
            VALUES
                ($appraisal_id, 'mechanical', 'Clutch work', 25000, $created_at)
            """,
            new { appraisal_id = id, created_at = now });
        connection.Execute(
            """
            INSERT INTO recon_line
                (appraisal_id, category, description, estimate, created_at)
            VALUES
                ($appraisal_id, 'body', 'Panel work', 15000, $created_at)
            """,
            new { appraisal_id = id, created_at = now });
        connection.Execute(
            """
            INSERT INTO recon_line
                (appraisal_id, category, description, estimate, created_at)
            VALUES
                ($appraisal_id, 'detail', 'Full detail', 2500, $created_at)
            """,
            new { appraisal_id = id, created_at = now });

        // Count children before delete.
        var before = connection.QuerySingle<int>(
            "SELECT COUNT(*) FROM recon_line WHERE appraisal_id = $id",
            new { id });
        Assert.Equal(3, before);

        // Delete the parent appraisal.
        connection.Execute("DELETE FROM appraisal WHERE id = $id", new { id });

        // Count children after delete — should be zero.
        var after = connection.QuerySingle<int>(
            "SELECT COUNT(*) FROM recon_line WHERE appraisal_id = $id",
            new { id });
        Assert.Equal(0, after);
    }
}
