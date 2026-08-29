using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DealDesk.Tests;

/// Phase D audit trail schema constraint tests — prove that `sql/003_audit.sql`'s
/// CHECK constraints reject bad data, that UPDATE/DELETE are refused on the
/// append-only audit table, and that the trail survives its parent's deletion.
/// Mirrors `WorksheetSchemaTests.cs` setup (TempDb, parameterised INSERT,
/// invented demo values) but asserts the audit-entry-specific invariants.
public sealed class AuditSchemaTests
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
    public void A_good_audit_row_inserts()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        // No exception means the insert succeeded.
        connection.Execute(
            """
            INSERT INTO audit_entry
                (appraisal_id, field, old_value, new_value, changed_by, reason, changed_at)
            VALUES
                ($appraisal_id, $field, $oldValue, $newValue, $changedBy, $reason, $changedAt)
            """,
            new
            {
                appraisal_id = id,
                field = "miles",
                oldValue = "74311",
                newValue = "74800",
                changedBy = "D. Okonjo",
                reason = "odometer reread",
                changedAt = now,
            });
    }

    [Fact]
    public void Empty_reason_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO audit_entry
                    (appraisal_id, field, old_value, new_value, changed_by, reason, changed_at)
                VALUES
                    ($appraisal_id, $field, $oldValue, $newValue, $changedBy, $reason, $changedAt)
                """,
                new
                {
                    appraisal_id = id,
                    field = "miles",
                    oldValue = "74311",
                    newValue = "74800",
                    changedBy = "D. Okonjo",
                    reason = "",
                    changedAt = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Empty_changed_by_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO audit_entry
                    (appraisal_id, field, old_value, new_value, changed_by, reason, changed_at)
                VALUES
                    ($appraisal_id, $field, $oldValue, $newValue, $changedBy, $reason, $changedAt)
                """,
                new
                {
                    appraisal_id = id,
                    field = "miles",
                    oldValue = "74311",
                    newValue = "74800",
                    changedBy = "",
                    reason = "odometer reread",
                    changedAt = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Identical_old_and_new_value_rejected()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute(
                """
                INSERT INTO audit_entry
                    (appraisal_id, field, old_value, new_value, changed_by, reason, changed_at)
                VALUES
                    ($appraisal_id, $field, $oldValue, $newValue, $changedBy, $reason, $changedAt)
                """,
                new
                {
                    appraisal_id = id,
                    field = "miles",
                    oldValue = "74311",
                    newValue = "74311",
                    changedBy = "D. Okonjo",
                    reason = "odometer reread",
                    changedAt = now,
                }));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Audit_entry_is_append_only()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        // Insert one good row first.
        connection.Execute(
            """
            INSERT INTO audit_entry
                (appraisal_id, field, old_value, new_value, changed_by, reason, changed_at)
            VALUES
                ($appraisal_id, $field, $oldValue, $newValue, $changedBy, $reason, $changedAt)
            """,
            new
            {
                appraisal_id = id,
                field = "miles",
                oldValue = "74311",
                newValue = "74800",
                changedBy = "D. Okonjo",
                reason = "odometer reread",
                changedAt = now,
            });

        // UPDATE must be refused.
        var updateEx = Assert.Throws<SqliteException>(() =>
            connection.Execute("UPDATE audit_entry SET reason = 'x'"));
        Assert.Contains("append-only", updateEx.Message);

        // DELETE must be refused.
        var deleteEx = Assert.Throws<SqliteException>(() =>
            connection.Execute("DELETE FROM audit_entry"));
        Assert.Contains("append-only", deleteEx.Message);
    }

    [Fact]
    public void Parent_delete_does_not_cascade_away_the_trail()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = CreateAppraisal(connection);

        // Insert one good audit row so the FK is exercised.
        connection.Execute(
            """
            INSERT INTO audit_entry
                (appraisal_id, field, old_value, new_value, changed_by, reason, changed_at)
            VALUES
                ($appraisal_id, $field, $oldValue, $newValue, $changedBy, $reason, $changedAt)
            """,
            new
            {
                appraisal_id = id,
                field = "miles",
                oldValue = "74311",
                newValue = "74800",
                changedBy = "D. Okonjo",
                reason = "odometer reread",
                changedAt = now,
            });

        // Deleting the parent appraisal must fail — the trail is not cascaded.
        var ex = Assert.Throws<SqliteException>(() =>
            connection.Execute("DELETE FROM appraisal WHERE id = $id", new { id }));
        Assert.Contains("FOREIGN KEY constraint failed", ex.Message);
    }
}
