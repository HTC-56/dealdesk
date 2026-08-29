using System.Linq;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// Seeded recon-variance, per worksheet. `SeedSmokeTests` asserts the
/// store-wide totals; this file asserts the ROWS underneath them.
///
/// One factory and client per fact, via `SeedSmokeTests.SeededClient`.
public sealed class SeedVarianceApiTests
{
    /// Worksheet 1: estimate 140000, actual 145500, variance 5500, no
    /// unposted lines.
    [Fact]
    public async Task Worksheet_1_has_all_posted_recon()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync(
            "/api/appraisals/1/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(140000L, doc.RootElement.GetProperty("totalEstimate").GetInt64());
        Assert.Equal(145500L, doc.RootElement.GetProperty("totalActual").GetInt64());
        Assert.Equal(5500L, doc.RootElement.GetProperty("totalVariance").GetInt64());
        Assert.Equal(0, doc.RootElement.GetProperty("unpostedLines").GetInt32());
    }

    /// Worksheet 1 byCategory: detail, mechanical, tires in name order
    /// with variances 0, 7500, −2000.
    [Fact]
    public async Task ByCategory_has_three_rows_with_both_signs()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync(
            "/api/appraisals/1/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var byCat = doc.RootElement.GetProperty("byCategory");

        Assert.Equal(3, byCat.GetArrayLength());
        Assert.Equal("detail", byCat[0].GetProperty("category").GetString());
        Assert.Equal(0L, byCat[0].GetProperty("variance").GetInt64());
        Assert.Equal("mechanical", byCat[1].GetProperty("category").GetString());
        Assert.Equal(7500L, byCat[1].GetProperty("variance").GetInt64());
        Assert.Equal("tires", byCat[2].GetProperty("category").GetString());
        Assert.Equal(-2000L, byCat[2].GetProperty("variance").GetInt64());
    }

    /// Worksheet 2 body category: two postings (90000 and −15000) against
    /// a 60000 estimate → actual 75000, variance 15000.
    [Fact]
    public async Task Credit_is_subtracted_not_ignored()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync(
            "/api/appraisals/2/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var byCat = doc.RootElement.GetProperty("byCategory");

        long bodyActual = 0, bodyVariance = 0;
        foreach (var row in byCat.EnumerateArray())
        {
            if (row.GetProperty("category").GetString() == "body")
            {
                bodyActual = row.GetProperty("actual").GetInt64();
                bodyVariance = row.GetProperty("variance").GetInt64();
                break;
            }
        }
        Assert.Equal(75000L, bodyActual);
        Assert.Equal(15000L, bodyVariance);
    }

    /// Worksheet 6: unfinished recon — variance −14000, 1 unposted,
    /// 3 lines, one with posted false.
    [Fact]
    public async Task Unfinished_recon_shows_unposted_lines()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync(
            "/api/appraisals/6/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(-14000L, doc.RootElement.GetProperty("totalVariance").GetInt64());
        Assert.Equal(1, doc.RootElement.GetProperty("unpostedLines").GetInt32());
        Assert.Equal(3, doc.RootElement.GetProperty("lines").GetArrayLength());

        var unposted = doc.RootElement.GetProperty("lines")
            .EnumerateArray()
            .Count(l => l.GetProperty("posted").GetBoolean() == false);
        Assert.Equal(1, unposted);
    }

    /// Worksheet 4 has nothing posted: actual 0, variance −150000,
    /// two unposted lines.
    [Fact]
    public async Task Nothing_posted_means_zero_actual()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync(
            "/api/appraisals/4/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(0L, doc.RootElement.GetProperty("totalActual").GetInt64());
        Assert.Equal(-150000L, doc.RootElement.GetProperty("totalVariance").GetInt64());
        Assert.Equal(2, doc.RootElement.GetProperty("unpostedLines").GetInt32());
    }

    /// Worksheet 12 has no recon lines at all: 200 with empty lines,
    /// zero totals — never a 404.
    [Fact]
    public async Task No_recon_lines_returns_empty_not_404()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.GetAsync(
            "/api/appraisals/12/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(0L, doc.RootElement.GetProperty("totalEstimate").GetInt64());
        Assert.Equal(0L, doc.RootElement.GetProperty("totalVariance").GetInt64());
        Assert.Equal(0, doc.RootElement.GetProperty("lines").GetArrayLength());
    }
}
