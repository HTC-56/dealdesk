using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// Front-gross report over HTTP: won worksheets, planned gross minus
/// recon overage. Each [Fact] gets its own factory.
public sealed class FrontGrossApiTests
{
    /// A draft-only worksheet: 200, empty rows, wonCount=0.
    [Fact]
    public async Task Draft_worksheet_returns_empty_report()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        await ReportSmokeTests.CreateAsync(client, "A. Whitfield");

        using var response = await client.GetAsync("/api/reports/front-gross");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("wonCount").GetInt32());
        Assert.Equal(0, root.GetProperty("rows").GetArrayLength());
    }

    /// Won worksheet, no recon: targetGross=150000, reconVariance=0,
    /// projectedGross=150000.
    [Fact]
    public async Task Won_worksheet_without_recon_shows_full_target()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(
            client, id, "appraised", "presented", "won");

        await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 150_000 });

        using var response = await client.GetAsync("/api/reports/front-gross");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(1, root.GetProperty("wonCount").GetInt32());

        var rows = root.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        var row = rows[0];
        Assert.Equal("A. Whitfield", row.GetProperty("appraiser").GetString());
        Assert.Equal(150_000, row.GetProperty("targetGross").GetInt64());
        Assert.Equal(0, row.GetProperty("reconVariance").GetInt64());
        Assert.Equal(150_000, row.GetProperty("projectedGross").GetInt64());
    }

    /// Recon overage (100k estimate, 140k posted): variance=40000,
    /// projectedGross drops to 110000.
    [Fact]
    public async Task Recon_overage_eats_projected_gross()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(
            client, id, "appraised", "presented", "won");

        await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 150_000 });

        var line = await ReconLineAsync(client, id, "paint", 100_000);
        await PostActualAsync(client, id, line, 140_000);

        using var response = await client.GetAsync("/api/reports/front-gross");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        var row = rows[0];
        Assert.Equal(40_000, row.GetProperty("reconVariance").GetInt64());
        Assert.Equal(110_000, row.GetProperty("projectedGross").GetInt64());
    }

    /// Recon under-spending adds to projection: 100k estimate, 60k posted
    /// gives variance=-40000, projectedGross=190000.
    [Fact]
    public async Task Recon_under_spending_boosts_projected_gross()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(
            client, id, "appraised", "presented", "won");

        await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 150_000 });

        var line = await ReconLineAsync(client, id, "mechanical", 100_000);
        await PostActualAsync(client, id, line, 60_000);

        using var response = await client.GetAsync("/api/reports/front-gross");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        var row = rows[0];
        Assert.Equal(-40_000, row.GetProperty("reconVariance").GetInt64());
        Assert.Equal(190_000, row.GetProperty("projectedGross").GetInt64());
    }

    /// Won worksheet with recon line and no postings: unpostedLines=1.
    [Fact]
    public async Task Unposted_recon_line_marks_projection_provisional()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(
            client, id, "appraised", "presented", "won");

        await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 150_000 });

        await ReconLineAsync(client, id, "tires", 80_000);

        using var response = await client.GetAsync("/api/reports/front-gross");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        var row = rows[0];
        Assert.Equal(1, row.GetProperty("unpostedLines").GetInt32());
    }

    /// Two appraisers, each with a won worksheet: rows in name order,
    /// top-level totalProjectedGross equals sum of rows' projectedGross.
    [Fact]
    public async Task Two_appraisers_in_name_order_with_correct_totals()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var b = await ReportSmokeTests.CreateAsync(client, "B. Ferreira");
        await ReportSmokeTests.MoveAsync(
            client, b, "appraised", "presented", "won");
        await client.PutAsJsonAsync(
            $"/api/appraisals/{b}/offer-inputs",
            new { pack = 0, targetGross = 100_000 });

        var a = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(
            client, a, "appraised", "presented", "won");
        await client.PutAsJsonAsync(
            $"/api/appraisals/{a}/offer-inputs",
            new { pack = 0, targetGross = 200_000 });

        using var response = await client.GetAsync("/api/reports/front-gross");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var rows = root.GetProperty("rows");

        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("A. Whitfield", rows[0].GetProperty("appraiser").GetString());
        Assert.Equal("B. Ferreira", rows[1].GetProperty("appraiser").GetString());

        var rowSum = rows[0].GetProperty("projectedGross").GetInt64()
                   + rows[1].GetProperty("projectedGross").GetInt64();
        Assert.Equal(rowSum, root.GetProperty("totalProjectedGross").GetInt64());
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
