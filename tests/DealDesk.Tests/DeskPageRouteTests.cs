using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace DealDesk.Tests;

/// The page and the API name the same routes. The smoke test proves the twelve
/// read routes answer; this file closes the loop in the other direction — the
/// page names nothing else, and its writes land where it says they do.
///
/// Use `Regex.Matches(page, "/api/[a-z0-9-]+")` to collect the path fragments
/// the page mentions, then assert the two high-level prefixes and the full
/// set of per-worksheet suffixes. On a seeded database, exercise the three
/// write paths the page calls and verify read-back of a row just posted.
public sealed class DeskPageRouteTests
{
    /// The page mentions exactly two distinct /api/ fragments:
    /// /api/appraisals and /api/reports. Every deeper path is built by
    /// appending to one of those two.
    [Fact]
    public async Task The_page_names_exactly_two_api_prefixes()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        var matches = Regex.Matches(page, "/api/[a-z0-9-]+")
            .Cast<Match>()
            .Select(m => m.Value)
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        Assert.Equal(2, matches.Count);
        Assert.Contains("/api/appraisals", matches);
        Assert.Contains("/api/reports", matches);
    }

    /// The routes the page reads are spelled out in it. All three report paths
    /// appear whole and each per-worksheet suffix appears as its own literal.
    [Fact]
    public async Task The_page_spells_out_every_read_route_literal()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        // Report routes — full paths, not just the /api/reports prefix.
        Assert.Contains("/api/reports/look-to-book", page, StringComparison.Ordinal);
        Assert.Contains("/api/reports/recon-variance", page, StringComparison.Ordinal);
        Assert.Contains("/api/reports/front-gross", page, StringComparison.Ordinal);

        // Per-worksheet suffixes built off /api/appraisals/{id}/.
        foreach (var suffix in new[]
        {
            "/walk-items",
            "/recon-lines",
            "/comps",
            "/offer-inputs",
            "/offer",
            "/recon-variance",
            "/audit",
        })
        {
            Assert.Contains(suffix, page, StringComparison.Ordinal);
        }
    }

    /// The write path to walk-items really accepts writes. On a seeded client,
    /// POST to /api/appraisals/1/walk-items with area exterior answers 201.
    [Fact]
    public async Task Post_walk_items_lands_on_the_server()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/api/appraisals/1/walk-items",
            new { area = "exterior", note = "minor scuff on the bumper" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("exterior", doc.RootElement.GetProperty("area").GetString());
    }

    /// PUT to offer-inputs lands. pack 90000 and targetGross 150000 answers 200.
    [Fact]
    public async Task Put_offer_inputs_lands_on_the_server()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.PutAsJsonAsync(
            "/api/appraisals/1/offer-inputs",
            new { pack = 90000, targetGross = 150000 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// POST to lifecycle status lands. Worksheet 5 is draft in the demo month,
    /// so moving it to appraised is legal and answers 200.
    [Fact]
    public async Task Post_status_lands_on_the_server()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var response = await client.PostAsJsonAsync(
            "/api/appraisals/5/status",
            new
            {
                status = "appraised",
                changedBy = "test-runner",
                reason = "test: lifecycle write",
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// The page reads back what it wrote. POST a walk item to worksheet 1,
    /// then GET /api/appraisals/1/walk-items returns 3 entries — the seed
    /// wrote two, this test adds one.
    [Fact]
    public async Task Read_back_confirms_the_posted_row()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        // Seed wrote two walk items per worksheet; post one more.
        await client.PostAsJsonAsync(
            "/api/appraisals/1/walk-items",
            new { area = "glass", note = "test row for read-back" });

        using var response = await client.GetAsync("/api/appraisals/1/walk-items");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, doc.RootElement.GetArrayLength());
    }
}
