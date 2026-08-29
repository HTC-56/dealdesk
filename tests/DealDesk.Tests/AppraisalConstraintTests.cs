using Dapper;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DealDesk.Tests;

/// Phase A constraint tests — prove that `sql/001_init.sql`'s four CHECK
/// constraints actually reject bad data. Mirrors `AppraisalRoundTripTests.cs`
/// setup (TempDb, parameterised INSERT, invented demo values) but asserts
/// that violating rows throw `SqliteException` instead of succeeding.
public sealed class AppraisalConstraintTests
{
    // Invented vehicle: no real VIN, no real make or model, no real person.
    private const string Vin = "ZZ9ZZ99Z9Z9000042";

    /// Insert a row into a fresh migrated scratch database using the same
    /// INSERT shape as `AppraisalRoundTripTests`.  Callers vary individual
    /// fields to target the constraint under test.
    private static void InsertRow(
        string vin, int year, string make, string model, string trim,
        int miles, string appraiser, string status)
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();
        var now = DateTimeOffset.UtcNow.ToString("O");
        connection.Execute(
            """
            INSERT INTO appraisal
                (vin, model_year, make, model, trim_level, miles, appraiser,
                 status, created_at, updated_at)
            VALUES
                ($vin, $year, $make, $model, $trim, $miles, $appraiser,
                 $status, $created_at, $updated_at)
            """,
            new
            {
                vin,
                year,
                make,
                model,
                trim,
                miles,
                appraiser,
                status,
                created_at = now,
                updated_at = now,
            });
    }

    [Fact]
    public void Status_outside_lifecycle_is_rejected()
    {
        var ex = Assert.Throws<SqliteException>(() =>
            InsertRow(Vin, 2019, "Meridian", "Trailhead", "SE", 74_311,
                      "A. Whitfield", "sold"));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Negative_miles_are_rejected()
    {
        var ex = Assert.Throws<SqliteException>(() =>
            InsertRow(Vin, 2019, "Meridian", "Trailhead", "SE", -1,
                      "A. Whitfield", "draft"));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void A_sixteen_char_VIN_is_rejected()
    {
        var ex = Assert.Throws<SqliteException>(() =>
            InsertRow("ZZ9ZZ99Z9Z900004", 2019, "Meridian", "Trailhead", "SE",
                      74_311, "A. Whitfield", "draft"));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Fact]
    public void Model_year_before_1900_is_rejected()
    {
        var ex = Assert.Throws<SqliteException>(() =>
            InsertRow(Vin, 1800, "Meridian", "Trailhead", "SE", 74_311,
                      "A. Whitfield", "draft"));
        Assert.Contains("CHECK constraint failed", ex.Message);
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("appraised")]
    [InlineData("presented")]
    [InlineData("won")]
    [InlineData("lost")]
    public void Each_valid_status_inserts(string status)
    {
        // No exception means the constraint accepted the row.
        InsertRow(Vin, 2019, "Meridian", "Trailhead", "SE", 74_311,
                  "A. Whitfield", status);
    }
}
