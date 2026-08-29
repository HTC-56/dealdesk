using Dapper;
using Xunit;

namespace DealDesk.Tests;

/// The Dapper round-trip Phase A exists to prove: migrated schema in, plain
/// SQL out, values identical. Later data tests mirror this shape — open a
/// TempDb, execute parameterised SQL, read the row back with QuerySingle.
public sealed class AppraisalRoundTripTests
{
    // Invented vehicle: no real VIN, no real make or model, no real person.
    private const string Vin = "ZZ9ZZ99Z9Z9000042";

    // Dapper materialises through init-only properties and maps snake_case
    // columns onto them (Db.RegisterTypeHandlers turns that on), so the SQL
    // selects real column names and nothing needs an alias.
    private sealed record AppraisalRow
    {
        public long Id { get; init; }

        public string Vin { get; init; } = string.Empty;

        public int ModelYear { get; init; }

        public string Make { get; init; } = string.Empty;

        public string Model { get; init; } = string.Empty;

        public string TrimLevel { get; init; } = string.Empty;

        public int Miles { get; init; }

        public string Appraiser { get; init; } = string.Empty;

        public string Status { get; init; } = string.Empty;

        public string CreatedAt { get; init; } = string.Empty;

        public string UpdatedAt { get; init; } = string.Empty;
    }

    [Fact]
    public void An_appraisal_row_survives_insert_and_select()
    {
        using var temp = TempDb.CreateMigrated();
        using var connection = temp.Db.Open();

        var now = DateTimeOffset.UtcNow.ToString("O");
        var id = connection.QuerySingle<long>(
            """
            INSERT INTO appraisal
                (vin, model_year, make, model, trim_level, miles, appraiser,
                 status, created_at, updated_at)
            VALUES
                ($vin, $year, $make, $model, $trim, $miles, $appraiser,
                 $status, $now, $now)
            RETURNING id;
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
                now,
            });

        var row = connection.QuerySingle<AppraisalRow>(
            """
            SELECT id, vin, model_year, make, model, trim_level,
                   miles, appraiser, status, created_at, updated_at
            FROM appraisal
            WHERE id = $id;
            """,
            new { id });

        Assert.Equal(id, row.Id);
        Assert.Equal(Vin, row.Vin);
        Assert.Equal(2019, row.ModelYear);
        Assert.Equal("Meridian", row.Make);
        Assert.Equal("Trailhead", row.Model);
        Assert.Equal("SE", row.TrimLevel);
        Assert.Equal(74_311, row.Miles);
        Assert.Equal("A. Whitfield", row.Appraiser);
        Assert.Equal("draft", row.Status);
        Assert.Equal(now, row.CreatedAt);
    }
}
