using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// Seeded reports, appraiser by appraiser. `SeedSmokeTests` asserts the
/// store-wide totals; this file asserts the ROWS underneath them.
///
/// One factory and client per fact, via `SeedSmokeTests.SeededClient`.
/// Reach a row with `doc.RootElement.GetProperty("rows")[0]`.
public sealed class SeedReportApiTests
{
    /// Look-to-book returns three rows ordered by appraiser name: Whitfield,
    /// Ferreira, Delacroix — regardless of which worksheet was created first.
    [Fact]
    public async Task Look_to_book_returns_three_rows_in_name_order()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        var rows = root.GetProperty("rows");
        Assert.Equal(3, rows.GetArrayLength());
        Assert.Equal("A. Whitfield", rows[0].GetProperty("appraiser").GetString());
        Assert.Equal("B. Ferreira", rows[1].GetProperty("appraiser").GetString());
        Assert.Equal("C. Delacroix", rows[2].GetProperty("appraiser").GetString());
    }

    /// Whitfield's first row: 5 looked, 4 appraised, 2 booked, 1 lost,
    /// 2 open, bookRateBps 5000.
    [Fact]
    public async Task First_row_looks_five_appraises_four_books_two()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = doc.RootElement.GetProperty("rows")[0];

        Assert.Equal("A. Whitfield", row.GetProperty("appraiser").GetString());
        Assert.Equal(5, row.GetProperty("looked").GetInt32());
        Assert.Equal(4, row.GetProperty("appraised").GetInt32());
        Assert.Equal(2, row.GetProperty("booked").GetInt32());
        Assert.Equal(1, row.GetProperty("lost").GetInt32());
        Assert.Equal(2, row.GetProperty("openWorksheets").GetInt32());
        Assert.Equal(5000, row.GetProperty("bookRateBps").GetInt32());
    }

    /// Ferreira's second row shows truncation and the lost-from-draft rule:
    /// looked 4, appraised 3 (not 4 — worksheet 8 was lost from draft),
    /// booked 1, bookRateBps 3333.
    [Fact]
    public async Task Second_row_truncates_rate_at_3333_bps()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = doc.RootElement.GetProperty("rows")[1];

        Assert.Equal("B. Ferreira", row.GetProperty("appraiser").GetString());
        Assert.Equal(4, row.GetProperty("looked").GetInt32());
        Assert.Equal(3, row.GetProperty("appraised").GetInt32());
        Assert.Equal(1, row.GetProperty("booked").GetInt32());
        Assert.Equal(3333, row.GetProperty("bookRateBps").GetInt32());
    }

    /// Delacroix's third row: 3 looked, 2 appraised, 1 booked, 0 lost,
    /// bookRateBps 5000.
    [Fact]
    public async Task Third_row_books_one_of_two_appraised()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync("/api/reports/look-to-book");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = doc.RootElement.GetProperty("rows")[2];

        Assert.Equal("C. Delacroix", row.GetProperty("appraiser").GetString());
        Assert.Equal(3, row.GetProperty("looked").GetInt32());
        Assert.Equal(2, row.GetProperty("appraised").GetInt32());
        Assert.Equal(1, row.GetProperty("booked").GetInt32());
        Assert.Equal(0, row.GetProperty("lost").GetInt32());
        Assert.Equal(5000, row.GetProperty("bookRateBps").GetInt32());
    }

    /// Front-gross has 3 rows. Whitfield: won 2, target 330000, recon
    /// variance +10500, projected 319500, 0 unposted.
    [Fact]
    public async Task Front_gross_first_row_over_recon()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync("/api/reports/front-gross");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = doc.RootElement.GetProperty("rows")[0];

        Assert.Equal("A. Whitfield", row.GetProperty("appraiser").GetString());
        Assert.Equal(2, row.GetProperty("wonCount").GetInt32());
        Assert.Equal(330000L, row.GetProperty("targetGross").GetInt64());
        Assert.Equal(10500L, row.GetProperty("reconVariance").GetInt64());
        Assert.Equal(319500L, row.GetProperty("projectedGross").GetInt64());
        Assert.Equal(0, row.GetProperty("unpostedLines").GetInt32());
    }

    /// Front-gross second row: Ferreira recon variance −14000, projected
    /// 164000 (above target 150000), 1 unposted line.
    [Fact]
    public async Task Front_gross_second_row_under_recon()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync("/api/reports/front-gross");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = doc.RootElement.GetProperty("rows")[1];

        Assert.Equal("B. Ferreira", row.GetProperty("appraiser").GetString());
        Assert.Equal(1, row.GetProperty("wonCount").GetInt32());
        Assert.Equal(150000L, row.GetProperty("targetGross").GetInt64());
        Assert.Equal(-14000L, row.GetProperty("reconVariance").GetInt64());
        Assert.Equal(164000L, row.GetProperty("projectedGross").GetInt64());
        Assert.Equal(1, row.GetProperty("unpostedLines").GetInt32());
    }
}
