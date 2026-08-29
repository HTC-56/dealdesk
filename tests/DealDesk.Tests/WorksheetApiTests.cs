using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// Recon-line and comp collections over real HTTP. Mirrors WalkItemApiTests:
/// one [Fact] per assertion, DeskAppFactory per test, and
/// CreateAppraisalAsync for the shared parent.
public sealed class WorksheetApiTests
{
    /// POSTing a recon line returns 201 and the stored row with category and
    /// estimate echoed back.
    [Fact]
    public async Task Post_recon_line_returns_201_with_category_and_estimate()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/recon-lines",
            new
            {
                category = "mechanical",
                description = "timing belt",
                estimate = 45_000,
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("mechanical", doc.RootElement.GetProperty("category").GetString());
        Assert.Equal(45_000, doc.RootElement.GetProperty("estimate").GetInt64());
        Assert.Equal(id, doc.RootElement.GetProperty("appraisalId").GetInt64());
    }

    /// A category outside the recon vocabulary is a 400 here, not a CHECK
    /// violation escaping SQLite as a 500.
    [Fact]
    public async Task Post_recon_line_with_unknown_category_returns_400()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/recon-lines",
            new
            {
                category = "wheels",
                description = "two bald fronts",
                estimate = 20_000,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// A negative estimate is a 400 — recon credits would turn subtraction
    /// into addition in the offer math.
    [Fact]
    public async Task Post_recon_line_with_negative_estimate_returns_400()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/recon-lines",
            new
            {
                category = "body",
                description = "dent on quarter panel",
                estimate = -1,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// POSTing a comp returns 201 and the body's price matches what was sent.
    [Fact]
    public async Task Post_comp_returns_201_with_price()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/comps",
            new
            {
                label = "2019 Trailhead LT",
                modelYear = 2019,
                miles = 71_000,
                price = 1_550_000,
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1_550_000, doc.RootElement.GetProperty("price").GetInt64());
        Assert.Equal("2019 Trailhead LT", doc.RootElement.GetProperty("label").GetString());
    }

    /// After posting one comp, GET returns an array of length 1 with the
    /// correct label.
    [Fact]
    public async Task Get_comps_lists_posted_rows()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/comps",
            new
            {
                label = "2019 Trailhead LT",
                modelYear = 2019,
                miles = 71_000,
                price = 1_550_000,
            });

        using var response = await client.GetAsync($"/api/appraisals/{id}/comps");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("2019 Trailhead LT", doc.RootElement[0].GetProperty("label").GetString());
    }

    /// An unknown appraisal is a 404 on both verbs for comps.
    [Fact]
    public async Task Comps_on_unknown_appraisal_return_404()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var listed = await client.GetAsync("/api/appraisals/9999/comps");
        Assert.Equal(HttpStatusCode.NotFound, listed.StatusCode);

        using var posted = await client.PostAsJsonAsync(
            "/api/appraisals/9999/comps",
            new
            {
                label = "some car",
                modelYear = 2020,
                miles = 50_000,
                price = 1_000_000,
            });
        Assert.Equal(HttpStatusCode.NotFound, posted.StatusCode);
    }
}
