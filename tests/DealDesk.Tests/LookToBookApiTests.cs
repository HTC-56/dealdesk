using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// Look-to-book report over HTTP: multi-appraiser ordering and per-appraiser
/// counts. Each [Fact] gets its own factory so no test sees another's rows.
public sealed class LookToBookApiTests
{
    /// Two appraisers with worksheets produce two rows, returned in name order
    /// (A before B), not creation order. Create B. Ferreira FIRST so the two
    /// orders diverge.
    [Fact]
    public async Task Two_appraisers_return_rows_in_name_order()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var b = await ReportSmokeTests.CreateAsync(client, "B. Ferreira");
        await ReportSmokeTests.MoveAsync(client, b, "appraised", "presented", "won");

        var a = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(client, a, "appraised", "presented", "won");

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var rows = root.GetProperty("rows");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal("A. Whitfield", rows[0].GetProperty("appraiser").GetString());
        Assert.Equal("B. Ferreira", rows[1].GetProperty("appraiser").GetString());
    }

    /// Each row's looked counts only that appraiser's own worksheets.
    [Fact]
    public async Task Looked_counts_per_appraiser_not_store_wide()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.CreateAsync(client, "B. Ferreira");

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");

        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal(2, rows[0].GetProperty("looked").GetInt32());
        Assert.Equal(1, rows[1].GetProperty("looked").GetInt32());
    }

    /// The top-level looked equals the sum of the rows' looked.
    [Fact]
    public async Task Top_level_looked_is_sum_of_rows()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.CreateAsync(client, "B. Ferreira");

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var rows = root.GetProperty("rows");

        var rowSum = rows[0].GetProperty("looked").GetInt32()
                   + rows[1].GetProperty("looked").GetInt32();
        Assert.Equal(rowSum, root.GetProperty("looked").GetInt32());
    }

    /// A won worksheet shows booked=1, openWorksheets=0, bookRateBps=10000.
    [Fact]
    public async Task Won_worksheet_shows_booked_and_full_rate()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(client, id, "appraised", "presented", "won");

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        var row = rows[0];
        Assert.Equal(1, row.GetProperty("booked").GetInt32());
        Assert.Equal(0, row.GetProperty("openWorksheets").GetInt32());
        Assert.Equal(10000, row.GetProperty("bookRateBps").GetInt32());
    }

    /// A draft worksheet counts in looked but not appraised; bookRateBps is 0.
    [Fact]
    public async Task Draft_worksheet_counts_in_looked_not_appraised()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        await ReportSmokeTests.CreateAsync(client, "A. Whitfield");

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        var row = rows[0];
        Assert.Equal(1, row.GetProperty("looked").GetInt32());
        Assert.Equal(0, row.GetProperty("appraised").GetInt32());
        Assert.Equal(0, row.GetProperty("bookRateBps").GetInt32());
    }

    /// Appraised+lost counts in both appraised and lost; lost-from-draft
    /// counts only in lost. Assert appraised=1 and lost=2.
    [Fact]
    public async Task Lost_from_appraised_counts_in_appraised_not_lost_from_draft()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        // Appraised then lost: counts in both appraised AND lost
        var late = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(client, late, "appraised", "lost");

        // Lost straight from draft: counts only in lost
        var early = await ReportSmokeTests.CreateAsync(client, "A. Whitfield");
        await ReportSmokeTests.MoveAsync(client, early, "lost");

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());

        var row = rows[0];
        Assert.Equal(2, row.GetProperty("looked").GetInt32());
        Assert.Equal(1, row.GetProperty("appraised").GetInt32());
        Assert.Equal(2, row.GetProperty("lost").GetInt32());
    }
}
