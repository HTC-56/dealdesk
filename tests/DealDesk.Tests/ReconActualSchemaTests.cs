using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DealDesk.Tests;

/// Phase E recon actuals schema constraint tests — prove that
/// `sql/004_recon_actual.sql`'s CHECK constraints reject zero-amount and blank
/// fields, that negative credits insert cleanly, and that postings cascade with
/// their recon line. Mirrors `AuditSchemaTests.cs` setup (TempDb, parameterised
/// INSERT, invented demo values) but asserts the recon-actual-specific invariants.
public sealed class ReconActualSchemaTests
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

    /// Insert one recon_line row and return its id via RETURNING id.
    private static long CreateReconLine(Microsoft.Data.Sqlite.SqliteConnection conn, long appraisalId)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        return conn.QuerySingle<long>(
            """
            INSERT INTO recon_line
                (appraisal_id, category, description, estimate, created_at)
            VALUES
                ($appraisal_id, $category, $description, $estimate, $created_at)
            RETURNING id
            """,
            new
            {
                appraisal_id = appraisalId,
                category = "paint",
                description = "respray rear quarter",
                estimate = 120_000,
                created_at = now,
            });
    }

    [Fact]
    public void A_good_posting_inserts()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var appraisalId = CreateAppraisal(connection);
        var lineId = CreateReconLine(connection, appraisalId);

        // No exception means the insert succeeded.
        connection.Execute(
            """
            INSERT INTO recon_actual
                (recon_line_id, amount, description, posted_by, posted_at)
            VALUES
                ($recon_line_id, $amount, $description, $postedBy, $postedAt)
            """,
            new
            {
                recon_line_id = lineId,
                amount = 90_000,
                description = "body shop invoice 4471",
                postedBy = "R. Vasquez",
                postedAt = now,
            });
    }

    [Fact]
    public void Zero_amount_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var appraisalId = CreateAppraisal(connection);
        var lineId = CreateReconLine(connection, appraisalId);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO recon_actual
                    (recon_line_id, amount, description, posted_by, posted_at)
                VALUES
                    ($recon_line_id, $amount, $description, $postedBy, $postedAt)
                """,
                new
                {
                    recon_line_id = lineId,
                    amount = 0,
                    description = "body shop invoice 4471",
                    postedBy = "R. Vasquez",
                    postedAt = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Empty_description_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var appraisalId = CreateAppraisal(connection);
        var lineId = CreateReconLine(connection, appraisalId);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO recon_actual
                    (recon_line_id, amount, description, posted_by, posted_at)
                VALUES
                    ($recon_line_id, $amount, $description, $postedBy, $postedAt)
                """,
                new
                {
                    recon_line_id = lineId,
                    amount = 90_000,
                    description = "",
                    postedBy = "R. Vasquez",
                    postedAt = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Empty_posted_by_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var appraisalId = CreateAppraisal(connection);
        var lineId = CreateReconLine(connection, appraisalId);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO recon_actual
                    (recon_line_id, amount, description, posted_by, posted_at)
                VALUES
                    ($recon_line_id, $amount, $description, $postedBy, $postedAt)
                """,
                new
                {
                    recon_line_id = lineId,
                    amount = 90_000,
                    description = "body shop invoice 4471",
                    postedBy = "",
                    postedAt = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void A_credit_inserts_without_exception()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var appraisalId = CreateAppraisal(connection);
        var lineId = CreateReconLine(connection, appraisalId);

        // No exception means a negative amount (credit) is legal.
        connection.Execute(
            """
            INSERT INTO recon_actual
                (recon_line_id, amount, description, posted_by, posted_at)
            VALUES
                ($recon_line_id, $amount, $description, $postedBy, $postedAt)
            """,
            new
            {
                recon_line_id = lineId,
                amount = -5_000,
                description = "core charge refund",
                postedBy = "R. Vasquez",
                postedAt = now,
            });
    }

    [Fact]
    public void Delete_recon_line_cascades_away_postings()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var appraisalId = CreateAppraisal(connection);
        var lineId = CreateReconLine(connection, appraisalId);

        // Insert two postings so the cascade is exercised.
        connection.Execute(
            """
            INSERT INTO recon_actual
                (recon_line_id, amount, description, posted_by, posted_at)
            VALUES
                ($recon_line_id, $amount, $description, $postedBy, $postedAt)
            """,
            new[]
            {
                new
                {
                    recon_line_id = lineId,
                    amount = 50_000,
                    description = "panel beater invoice",
                    postedBy = "R. Vasquez",
                    postedAt = now,
                },
                new
                {
                    recon_line_id = lineId,
                    amount = 40_000,
                    description = "paint and materials",
                    postedBy = "R. Vasquez",
                    postedAt = now,
                },
            });

        // Delete the line; postings should cascade away.
        connection.Execute("DELETE FROM recon_line WHERE id = $lineId", new { lineId });

        // No postings should remain.
        var count = connection.QuerySingle<int>(
            "SELECT COUNT(*) FROM recon_actual WHERE recon_line_id = $lineId",
            new { lineId });
        Assert.Equal(0, count);
    }
}
