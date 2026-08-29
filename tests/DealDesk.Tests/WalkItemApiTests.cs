using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// The condition-walk collection over real HTTP. This pair of routes is the
/// template the recon-line and comp collections copy, so the four behaviours
/// pinned here — created row echoed back, list ordered oldest first, bad
/// vocabulary refused, unknown parent 404 — are the contract all three share.
public sealed class WalkItemApiTests
{
    /// Posting a walk line returns 201 and the stored row, defaults applied.
    [Fact]
    public async Task Post_walk_item_returns_201_with_the_stored_row()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/walk-items",
            new { area = "tires", note = "fronts near the wear bars" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("tires", doc.RootElement.GetProperty("area").GetString());

        // Severity was omitted, so the column default is what came back.
        Assert.Equal("minor", doc.RootElement.GetProperty("severity").GetString());
        Assert.Equal(id, doc.RootElement.GetProperty("appraisalId").GetInt64());
    }

    /// The list serves this appraisal's lines, oldest first.
    [Fact]
    public async Task Get_walk_items_lists_this_appraisals_lines_oldest_first()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/walk-items", new { area = "glass", note = "chip" });
        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/walk-items",
            new { area = "interior", severity = "moderate", note = "seat tear" });

        using var response = await client.GetAsync($"/api/appraisals/{id}/walk-items");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("glass", doc.RootElement[0].GetProperty("area").GetString());
        Assert.Equal("moderate", doc.RootElement[1].GetProperty("severity").GetString());
    }

    /// An area outside the vocabulary is a 400 here, never a CHECK violation
    /// escaping SQLite as a 500.
    [Fact]
    public async Task Post_walk_item_with_unknown_area_returns_400()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/walk-items", new { area = "undercarriage" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// An unknown appraisal is a 404 on both verbs — not an empty list, and not
    /// a foreign-key failure on the way in.
    [Fact]
    public async Task Walk_items_on_unknown_appraisal_return_404()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var listed = await client.GetAsync("/api/appraisals/9999/walk-items");
        Assert.Equal(HttpStatusCode.NotFound, listed.StatusCode);

        using var posted = await client.PostAsJsonAsync(
            "/api/appraisals/9999/walk-items", new { area = "tires" });
        Assert.Equal(HttpStatusCode.NotFound, posted.StatusCode);
    }

    /// Creates one draft appraisal through the API and returns its id.
    internal static async Task<long> CreateAppraisalAsync(HttpClient client)
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
                appraiser = "A. Whitfield",
            });

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt64();
    }
}
