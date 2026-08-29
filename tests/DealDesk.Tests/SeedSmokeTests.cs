using System.Net;
using System.Text.Json;
using Dapper;
using DealDesk.Data;
using DealDesk.Domain;
using Xunit;

namespace DealDesk.Tests;

/// The demo month of sql/seed.sql — SPEC.md feature 8 — end to end: written
/// once, refused twice, and read back through the same routes a demo would use.
///
/// The seed exists so a fresh clone has something to show. That makes its
/// content a contract, not decoration: twelve worksheets across three
/// appraisers, all five lifecycle states, every recon category, both signs of
/// variance, and one worksheet nobody can price yet. The later seed test files
/// mirror this one and reuse its helpers.
public sealed class SeedSmokeTests
{
    /// What one run of the seed writes. The demo month is fixed data, so these
    /// are facts about the script rather than magic numbers.
    internal const int Worksheets = 12;
    internal const int WalkItems = 24;
    internal const int Comps = 29;
    internal const int ReconLines = 15;
    internal const int ReconActuals = 11;
    internal const int OfferInputs = 9;
    internal const int AuditEntries = 24;

    /// Worksheet 1 is the fully worked example: comps averaging $18,500, recon
    /// estimated at $1,400, a $900 pack and $1,500 of target gross.
    internal const long FirstAnchor = 1_850_000;
    internal const long FirstRecommended = 1_470_000;

    /// Ids are explicit in the seed, so a test can name a worksheet. Worksheet
    /// 12 is the car that just landed: walked, but no comps yet.
    internal const long UnpriceableWorksheet = 12;

    /// The three invented appraisers, in the order every report sorts them.
    internal static readonly string[] Appraisers =
        ["A. Whitfield", "B. Ferreira", "C. Delacroix"];

    /// The whole month lands in one call, and a second call changes nothing.
    /// "Already seeded" is the normal state of a database somebody has demoed,
    /// so it answers 0 rather than throwing or writing a second month.
    [Fact]
    public void Seed_writes_the_month_once_and_declines_to_write_it_twice()
    {
        using var temp = TempDb.CreateMigrated();

        Assert.Equal(Worksheets, Seeder.Apply(temp.Db));
        Assert.Equal(0, Seeder.Apply(temp.Db));

        using var connection = temp.Db.Open();
        Assert.Equal(Worksheets, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM appraisal;"));
        Assert.Equal(WalkItems, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM walk_item;"));
        Assert.Equal(Comps, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM comp;"));
        Assert.Equal(ReconLines, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recon_line;"));
        Assert.Equal(
            ReconActuals, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recon_actual;"));
        Assert.Equal(
            OfferInputs, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM offer_input;"));
        Assert.Equal(
            AuditEntries, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM audit_entry;"));
    }

    /// The demo data obeys the same rules the API enforces on a caller: every
    /// VIN carries its own check digit, and every lifecycle state is occupied so
    /// the page and the gauge have all five to show.
    [Fact]
    public void Seeded_worksheets_are_valid_and_cover_every_lifecycle_state()
    {
        using var temp = SeededDb();
        using var connection = temp.Db.Open();

        foreach (var vin in connection.Query<string>("SELECT vin FROM appraisal;"))
        {
            Assert.True(Vin.IsValid(vin), vin + " is not a valid VIN");
        }

        var byStatus = connection.Query<(string Status, int Count)>(
            "SELECT status, COUNT(*) AS count FROM appraisal GROUP BY status ORDER BY status;")
            .ToDictionary(row => row.Status, row => row.Count, StringComparer.Ordinal);

        Assert.Equal(4, byStatus["won"]);
        Assert.Equal(2, byStatus["lost"]);
        Assert.Equal(2, byStatus["presented"]);
        Assert.Equal(2, byStatus["appraised"]);
        Assert.Equal(2, byStatus["draft"]);
    }

    /// All three reports read the seeded month rather than an empty store —
    /// which is the entire point of shipping demo data. The look-to-book rate
    /// is 4 booked over 9 appraised: 44.44%, in basis points.
    [Fact]
    public async Task The_three_reports_read_the_seeded_month()
    {
        using var factory = new DeskAppFactory();
        using var client = SeededClient(factory);

        using (var doc = await ReadAsync(client, "/api/reports/look-to-book"))
        {
            var root = doc.RootElement;
            Assert.Equal(12, root.GetProperty("looked").GetInt32());
            Assert.Equal(9, root.GetProperty("appraised").GetInt32());
            Assert.Equal(4, root.GetProperty("booked").GetInt32());
            Assert.Equal(4444, root.GetProperty("bookRateBps").GetInt32());
            Assert.Equal(3, root.GetProperty("rows").GetArrayLength());
        }

        using (var doc = await ReadAsync(client, "/api/reports/recon-variance"))
        {
            var root = doc.RootElement;
            Assert.Equal(679_000, root.GetProperty("totalEstimate").GetInt64());
            Assert.Equal(474_000, root.GetProperty("totalActual").GetInt64());
            Assert.Equal(-205_000, root.GetProperty("totalVariance").GetInt64());
            Assert.Equal(6, root.GetProperty("unpostedLines").GetInt32());

            // All eight recon categories are exercised by the demo month.
            Assert.Equal(8, root.GetProperty("rows").GetArrayLength());
        }

        using (var doc = await ReadAsync(client, "/api/reports/front-gross"))
        {
            var root = doc.RootElement;
            Assert.Equal(4, root.GetProperty("wonCount").GetInt32());
            Assert.Equal(645_000, root.GetProperty("totalTargetGross").GetInt64());
            Assert.Equal(-5_000, root.GetProperty("totalReconVariance").GetInt64());
            Assert.Equal(650_000, root.GetProperty("totalProjectedGross").GetInt64());
        }
    }

    /// A seeded worksheet prices exactly as a hand-built one does, derivation
    /// and all — and the one worksheet with no comps still answers the 409 an
    /// unpriceable worksheet has answered since Phase C.
    [Fact]
    public async Task A_seeded_worksheet_prices_and_the_unpriced_one_still_409s()
    {
        using var factory = new DeskAppFactory();
        using var client = SeededClient(factory);

        using (var doc = await ReadAsync(client, "/api/appraisals/1/offer"))
        {
            var root = doc.RootElement;
            Assert.Equal(FirstAnchor, root.GetProperty("anchor").GetInt64());
            Assert.Equal(3, root.GetProperty("compCount").GetInt32());
            Assert.Equal(140_000, root.GetProperty("recon").GetInt64());
            Assert.Equal(FirstRecommended, root.GetProperty("recommended").GetInt64());

            var derivation = root.GetProperty("derivation");
            Assert.Equal(4, derivation.GetArrayLength());

            long summed = 0;
            foreach (var step in derivation.EnumerateArray())
            {
                summed += step.GetProperty("amount").GetInt64();
            }

            Assert.Equal(FirstRecommended, summed);
        }

        using var unpriceable = await client.GetAsync(
            $"/api/appraisals/{UnpriceableWorksheet}/offer");
        Assert.Equal(HttpStatusCode.Conflict, unpriceable.StatusCode);
    }

    /// A migrated temp database carrying the demo month. Unit-shaped seed tests
    /// take one of these instead of booting the app.
    internal static TempDb SeededDb()
    {
        var temp = TempDb.CreateMigrated();
        Seeder.Apply(temp.Db);
        return temp;
    }

    /// A client onto an app whose database already holds the demo month. Seed
    /// before the client so no request ever sees a half-written store.
    internal static HttpClient SeededClient(DeskAppFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        factory.Seed();
        return factory.CreateClient();
    }

    /// GETs a route that must succeed and hands back its parsed body. Dispose
    /// the document, as every other JSON assertion in the suite does.
    internal static async Task<JsonDocument> ReadAsync(HttpClient client, string path)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }
}
