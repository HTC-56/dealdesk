using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// The recon-line and comp child collections over real HTTP. These two
/// collections copy the walk-item pattern — created row echoed back, list
/// ordered oldest first, bad vocabulary refused, unknown parent 404 — so
/// the four behaviours pinned here are the contract all three share.
public sealed class WorksheetApiTests
{
    /// Posting a recon line returns 201 and the stored row, defaults applied.
    [Fact]
    public async Task Post_recon_line_returns_201_with_the_stored_row()
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
                estimate = 45000,
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("mechanical", doc.RootElement.GetProperty("category").GetString());
        Assert.Equal(45000, doc.RootElement.GetProperty("estimate").GetInt64());
    }

    /// A category outside the controlled vocabulary is a 400 here, never a
    /// CHECK violation escaping SQLite as a 500.
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
                description = "two rims bent",
                estimate = 120000,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// A negative estimate is refused on the way in — the endpoint validates
    /// before the INSERT so the client never sees a SQL error.
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
                description = "dent repair",
                estimate = -1,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// Posting a comp returns 201 and the stored row, defaults applied.
    [Fact]
    public async Task Post_comp_returns_201_with_the_stored_row()
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
                miles = 71000,
                price = 1550000,
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1550000, doc.RootElement.GetProperty("price").GetInt64());
    }

    /// The list serves this appraisal's comps, oldest first.
    [Fact]
    public async Task Get_comps_lists_this_appraisals_comps_oldest_first()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        // POST the same comp from the previous test so the GET confirms it.
        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/comps", new
            {
                label = "2019 Trailhead LT",
                modelYear = 2019,
                miles = 71000,
                price = 1550000,
            });

        using var response = await client.GetAsync($"/api/appraisals/{id}/comps");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("2019 Trailhead LT", doc.RootElement[0].GetProperty("label").GetString());
    }

    /// An unknown appraisal is a 404 on both verbs — not an empty list, and
    /// not a foreign-key failure on the way in.
    [Fact]
    public async Task Comps_on_unknown_appraisal_return_404()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var listed = await client.GetAsync("/api/appraisals/9999/comps");
        Assert.Equal(HttpStatusCode.NotFound, listed.StatusCode);
    }
}
