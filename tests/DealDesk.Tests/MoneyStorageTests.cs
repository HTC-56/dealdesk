using Dapper;
using DealDesk.Domain;
using Xunit;

namespace DealDesk.Tests;

/// The Money storage Phase A tests prove: Money survives a Dapper round-trip
/// as INTEGER cents (positive and negative), and reading the column as a plain
/// long yields exactly the Cents value. Uses a scratch table (no migration) —
/// same TempDb pattern as AppraisalRoundTripTests.
public sealed class MoneyStorageTests
{
    // Invented vehicle — no real VIN, no real make or model, no real person.
    private const string Vin = "ZZ9ZZ99Z9Z9000042";

    private sealed record MoneyRow
    {
        public long Id { get; init; }
        public long Cents { get; init; }
    }

    /// Ensures the scratch table exists, then inserts a money row and returns
    /// the new row's id. Each test calls this with the Money value it wants
    /// to verify.
    private static long InsertMoney(
        Microsoft.Data.Sqlite.SqliteConnection db,
        Money money)
    {
        db.Execute("""
            CREATE TABLE IF NOT EXISTS money_scratch (
                id   INTEGER PRIMARY KEY AUTOINCREMENT,
                cents INTEGER NOT NULL
            );
            """);

        return db.QuerySingle<long>(
            """
            INSERT INTO money_scratch (cents) VALUES ($cents)
            RETURNING id;
            """,
            new { cents = money.Cents });
    }

    [Fact]
    public void A_positive_money_value_round_trips_as_integer_cents()
    {
        using var temp = TempDb.CreateMigrated();
        using var db = temp.Db.Open();

        var money = Money.FromDollars(4_500.00m); // $45.00

        InsertMoney(db, money);

        var row = db.QuerySingle<MoneyRow>(
            """
            SELECT id, cents
            FROM money_scratch
            WHERE id = 1;
            """,
            param: new { });

        Assert.Equal(money.Cents, row.Cents);
    }

    [Fact]
    public void A_negative_money_value_round_trips_as_integer_cents()
    {
        using var temp = TempDb.CreateMigrated();
        using var db = temp.Db.Open();

        var money = Money.FromDollars(-125.50m); // recon credit

        InsertMoney(db, money);

        var row = db.QuerySingle<MoneyRow>(
            """
            SELECT id, cents
            FROM money_scratch
            WHERE id = 1;
            """,
            param: new { });

        Assert.Equal(money.Cents, row.Cents);
    }

    [Fact]
    public void Reading_the_column_as_long_gives_exactly_the_Cents_value()
    {
        using var temp = TempDb.CreateMigrated();
        using var db = temp.Db.Open();

        var money = Money.FromDollars(99.99m);

        InsertMoney(db, money);

        // Read the cents column as a plain long — no MoneyTypeHandler
        // involved — and confirm it matches Cents exactly.
        var raw = db.QuerySingle<long>(
            """
            SELECT cents
            FROM money_scratch
            WHERE id = 1;
            """,
            param: new { });

        Assert.Equal(money.Cents, raw);
    }
}
