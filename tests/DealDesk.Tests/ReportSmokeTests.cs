using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using DealDesk.Api;
using DealDesk.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DealDesk.Tests;

/// The three report views of sql/005_reports.sql, end to end: worksheets in,
/// a director's reading out.
///
/// Every worksheet here is built over HTTP — created, walked through its
/// lifecycle, given recon lines and postings — because the reports exist to
/// summarise what the API actually wrote. The one exception is
/// `report_front_gross`, which is queried straight through Db: it proves the
/// view and its column mapping before that report has an endpoint of its own,
/// the same way OfferSmokeTests.cs priced a worksheet before the comp and
/// recon collections had routes.
public sealed class ReportSmokeTests
{
    /// Four worksheets for one appraiser: one bought, one lost before anyone
    /// priced it, one lost after appraisal, one still in draft. Look-to-book
    /// counts them apart, and the rate is booked over APPRAISED — the one lost
    /// from draft was never a real look at the car, and only the audit trail
    /// can tell the two lost worksheets apart.
    [Fact]
    public async Task Look_to_book_counts_the_month_and_rates_it_against_appraised()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var bought = await CreateAsync(client, "A. Whitfield");
        await MoveAsync(client, bought, "appraised", "presented", "won");

        var walkedEarly = await CreateAsync(client, "A. Whitfield");
        await MoveAsync(client, walkedEarly, "lost");

        var walkedLate = await CreateAsync(client, "A. Whitfield");
        await MoveAsync(client, walkedLate, "appraised", "lost");

        await CreateAsync(client, "A. Whitfield");

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(4, root.GetProperty("looked").GetInt32());
        Assert.Equal(2, root.GetProperty("appraised").GetInt32());
        Assert.Equal(1, root.GetProperty("booked").GetInt32());
        Assert.Equal(2, root.GetProperty("lost").GetInt32());
        Assert.Equal(1, root.GetProperty("openWorksheets").GetInt32());

        // One booked out of two appraised: 50.00%, in basis points.
        Assert.Equal(5000, root.GetProperty("bookRateBps").GetInt32());

        var rows = root.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("A. Whitfield", rows[0].GetProperty("appraiser").GetString());
        Assert.Equal(5000, rows[0].GetProperty("bookRateBps").GetInt32());
    }

    /// Recon variance reads across worksheets, not down one. Two cars, three
    /// lines, and the categories roll up in name order with the same
    /// actual − estimate sign the per-worksheet variance uses.
    [Fact]
    public async Task Recon_variance_report_rolls_every_worksheet_up_by_category()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var first = await CreateAsync(client, "A. Whitfield");
        var paint = await ReconLineAsync(client, first, "paint", 120_000);
        await PostActualAsync(client, first, paint, 90_000);
        await PostActualAsync(client, first, paint, 45_000);
        await ReconLineAsync(client, first, "tires", 80_000);

        var second = await CreateAsync(client, "B. Ferreira");
        var otherPaint = await ReconLineAsync(client, second, "paint", 60_000);
        await PostActualAsync(client, second, otherPaint, 50_000);

        using var response = await client.GetAsync("/api/reports/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(260_000, root.GetProperty("totalEstimate").GetInt64());
        Assert.Equal(185_000, root.GetProperty("totalActual").GetInt64());
        Assert.Equal(-75_000, root.GetProperty("totalVariance").GetInt64());
        Assert.Equal(1, root.GetProperty("unpostedLines").GetInt32());

        var rows = root.GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());

        // Paint: two lines across two cars, 180,000 estimated, 185,000 spent.
        Assert.Equal("paint", rows[0].GetProperty("category").GetString());
        Assert.Equal(2, rows[0].GetProperty("lineCount").GetInt32());
        Assert.Equal(5_000, rows[0].GetProperty("variance").GetInt64());
        Assert.Equal(0, rows[0].GetProperty("unpostedLines").GetInt32());

        // Tires: estimated and never posted against, so the variance is the
        // whole estimate and unpostedLines says why.
        Assert.Equal("tires", rows[1].GetProperty("category").GetString());
        Assert.Equal(-80_000, rows[1].GetProperty("variance").GetInt64());
        Assert.Equal(1, rows[1].GetProperty("unpostedLines").GetInt32());
    }

    /// The front-gross view, read directly. A won worksheet planned $1,500 of
    /// front gross and its recon ran $400 over, so the projection is $1,100.
    [Fact]
    public async Task Front_gross_view_takes_recon_overage_off_the_planned_gross()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAsync(client, "B. Ferreira");
        await MoveAsync(client, id, "appraised", "presented", "won");

        using var saved = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 90_000, targetGross = 150_000 });
        saved.EnsureSuccessStatusCode();

        var line = await ReconLineAsync(client, id, "mechanical", 100_000);
        await PostActualAsync(client, id, line, 90_000);
        await PostActualAsync(client, id, line, 50_000);

        using var connection = factory.Services.GetRequiredService<Db>().Open();
        var row = connection.QuerySingle<FrontGrossRow>(
            "SELECT " + ReportEndpoints.FrontGrossColumns +
            " FROM report_front_gross ORDER BY appraiser;");

        Assert.Equal("B. Ferreira", row.Appraiser);
        Assert.Equal(1, row.WonCount);
        Assert.Equal(150_000, row.TargetGross);
        Assert.Equal(40_000, row.ReconVariance);
        Assert.Equal(110_000, row.ProjectedGross);
        Assert.Equal(0, row.UnpostedLines);
    }

    /// A store with nothing in it is not an error. Both report routes answer
    /// 200 with zeroed totals and no rows — an empty sum is genuinely zero,
    /// exactly as it is for a worksheet with no recon lines. A won worksheet
    /// nobody appraised leaves the rate at zero rather than dividing by it.
    [Fact]
    public async Task Empty_store_reports_zeroed_totals_and_no_rows()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var lookToBook = await client.GetAsync("/api/reports/look-to-book");
        lookToBook.EnsureSuccessStatusCode();

        using var lookDoc = JsonDocument.Parse(await lookToBook.Content.ReadAsStringAsync());
        Assert.Equal(0, lookDoc.RootElement.GetProperty("looked").GetInt32());
        Assert.Equal(0, lookDoc.RootElement.GetProperty("bookRateBps").GetInt32());
        Assert.Equal(0, lookDoc.RootElement.GetProperty("rows").GetArrayLength());

        using var recon = await client.GetAsync("/api/reports/recon-variance");
        recon.EnsureSuccessStatusCode();

        using var reconDoc = JsonDocument.Parse(await recon.Content.ReadAsStringAsync());
        Assert.Equal(0, reconDoc.RootElement.GetProperty("totalEstimate").GetInt64());
        Assert.Equal(0, reconDoc.RootElement.GetProperty("totalVariance").GetInt64());
        Assert.Equal(0, reconDoc.RootElement.GetProperty("rows").GetArrayLength());
    }

    /// Creates a draft worksheet under the named appraiser and returns its id.
    /// Reports group by appraiser, so unlike the other API tests this one needs
    /// to choose the name.
    internal static async Task<long> CreateAsync(HttpClient client, string appraiser)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var response = await client.PostAsJsonAsync(
            "/api/appraisals",
            new
            {
                vin = "ZZ9ZZ99Z2Z9000042",
                modelYear = 2019,
                make = "Meridian",
                model = "Trailhead",
                trimLevel = "LT",
                miles = 74_311,
                appraiser,
            });

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt64();
    }

    /// Walks the worksheet through each status in turn, leaving the audit trail
    /// the look-to-book view reads.
    internal static async Task MoveAsync(HttpClient client, long id, params string[] statuses)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(statuses);

        foreach (var status in statuses)
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/appraisals/{id}/status",
                new { status, changedBy = "D. Okonjo", reason = "report fixture" });

            response.EnsureSuccessStatusCode();
        }
    }

    private static async Task<long> ReconLineAsync(
        HttpClient client, long appraisalId, string category, long estimate)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines",
            new { category, description = "seeded line", estimate });

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt64();
    }

    private static async Task PostActualAsync(
        HttpClient client, long appraisalId, long lineId, long amount)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals",
            new { amount, description = "shop invoice", postedBy = "R. Vasquez" });

        response.EnsureSuccessStatusCode();
    }
}
