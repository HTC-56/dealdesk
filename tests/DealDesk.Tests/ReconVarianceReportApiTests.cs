using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// Recon variance report over HTTP: cross-worksheet category rollup and
/// unposted-line tracking. Each [Fact] gets its own factory.
public sealed class ReconVarianceReportApiTests
{
    /// Worksheets with no recon lines: 200, empty rows, zero totals.
    [Fact]
    public async Task Store_with_no_recon_lines_returns_empty_report()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(
            client,
            await ReportSmokeTests.CreateAsync(client, "A. Whitfield"),
            "appraised", "presented", "won");

        using var response = await client.GetAsync("/api/reports/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("totalEstimate").GetInt64());
        Assert.Equal(0, root.GetProperty("rows").GetArrayLength());
    }

    /// One unposted mechanical line: estimate=100000, actual=0,
    /// variance=-100000, unpostedLines=1.
    [Fact]
    public async Task Unposted_line_shows_negative_variance()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        var line = await ReconLineAsync(client, id, "mechanical", 100_000);

        using var response = await client.GetAsync("/api/reports/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var rows = root.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("mechanical", rows[0].GetProperty("category").GetString());
        Assert.Equal(100_000, rows[0].GetProperty("estimate").GetInt64());
        Assert.Equal(0, rows[0].GetProperty("actual").GetInt64());
        Assert.Equal(-100_000, rows[0].GetProperty("variance").GetInt64());
        Assert.Equal(1, rows[0].GetProperty("unpostedLines").GetInt32());
    }

    /// Posting 140_000 against a 100_000 line flips variance to +40_000.
    [Fact]
    public async Task Over_posting_flips_variance_positive()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        var line = await ReconLineAsync(client, id, "paint", 100_000);
        await PostActualAsync(client, id, line, 140_000);

        using var response = await client.GetAsync("/api/reports/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        Assert.Equal(100_000, rows[0].GetProperty("estimate").GetInt64());
        Assert.Equal(140_000, rows[0].GetProperty("actual").GetInt64());
        Assert.Equal(40_000, rows[0].GetProperty("variance").GetInt64());
        Assert.Equal(0, rows[0].GetProperty("unpostedLines").GetInt32());
    }

    /// Two postings sum into one actual; lineCount stays 1.
    [Fact]
    public async Task Multiple_postings_sum_into_single_actual()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        var line = await ReconLineAsync(client, id, "tires", 100_000);
        await PostActualAsync(client, id, line, 90_000);
        await PostActualAsync(client, id, line, 50_000);

        using var response = await client.GetAsync("/api/reports/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        Assert.Equal(140_000, rows[0].GetProperty("actual").GetInt64());
        Assert.Equal(1, rows[0].GetProperty("lineCount").GetInt32());
        Assert.Equal(40_000, rows[0].GetProperty("variance").GetInt64());
    }

    /// A credit (negative posting) reduces actual: 90_000 then -15_000 = 75_000.
    [Fact]
    public async Task Credit_reduces_actual_amount()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        var line = await ReconLineAsync(client, id, "mechanical", 100_000);
        await PostActualAsync(client, id, line, 90_000);
        await PostActualAsync(client, id, line, -15_000);

        using var response = await client.GetAsync("/api/reports/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        Assert.Equal(75_000, rows[0].GetProperty("actual").GetInt64());
        Assert.Equal(-25_000, rows[0].GetProperty("variance").GetInt64());
    }

    /// Three categories come back in name order (body, detail, tires).
    /// Top-level totalEstimate equals sum of rows' estimate.
    [Fact]
    public async Task Categories_return_in_name_order_with_correct_totals()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReconLineAsync(client, id, "tires", 80_000);
        await ReconLineAsync(client, id, "body", 60_000);
        await ReconLineAsync(client, id, "detail", 40_000);

        using var response = await client.GetAsync("/api/reports/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var rows = root.GetProperty("rows");

        Assert.Equal(3, rows.GetArrayLength());
        Assert.Equal("body", rows[0].GetProperty("category").GetString());
        Assert.Equal("detail", rows[1].GetProperty("category").GetString());
        Assert.Equal("tires", rows[2].GetProperty("category").GetString());

        var rowSum = rows[0].GetProperty("estimate").GetInt64()
                   + rows[1].GetProperty("estimate").GetInt64()
                   + rows[2].GetProperty("estimate").GetInt64();
        Assert.Equal(rowSum, root.GetProperty("totalEstimate").GetInt64());
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
