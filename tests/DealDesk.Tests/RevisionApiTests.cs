using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// Field revisions over HTTP — the PATCH /api/appraisals/{id} route.
/// These six assertions prove the trail records one row per field, that
/// no-op writes are silent, and that validation rejects empty bodies.
public sealed class RevisionApiTests
{
    /// PATCHing miles returns 200 and echoes the new value.
    [Fact]
    public async Task Patch_miles_returns_200_with_new_value()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        using var response = await client.PatchAsJsonAsync(
            $"/api/appraisals/{id}",
            new { miles = 74_800, changedBy = "D. Okonjo", reason = "odometer reread" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(74_800, doc.RootElement.GetProperty("miles").GetInt32());
    }

    /// After one field revision the trail has one entry with correct fields.
    [Fact]
    public async Task After_field_revision_the_trail_has_one_entry()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        await client.PatchAsJsonAsync(
            $"/api/appraisals/{id}",
            new { miles = 74_800, changedBy = "D. Okonjo", reason = "odometer reread" });

        using var response = await client.GetAsync($"/api/appraisals/{id}/audit");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("miles", doc.RootElement[0].GetProperty("field").GetString());
        Assert.Equal("74311", doc.RootElement[0].GetProperty("oldValue").GetString());
        Assert.Equal("74800", doc.RootElement[0].GetProperty("newValue").GetString());
    }

    /// Revising miles and appraiser in ONE call writes TWO trail entries.
    [Fact]
    public async Task Patching_two_fields_writes_two_trail_entries()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        await client.PatchAsJsonAsync(
            $"/api/appraisals/{id}",
            new
            {
                miles = 74_800,
                appraiser = "B. Chen",
                changedBy = "D. Okonjo",
                reason = "corrections",
            });

        using var response = await client.GetAsync($"/api/appraisals/{id}/audit");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, doc.RootElement.GetArrayLength());
    }

    /// Revising miles to the value it already holds writes NO trail entry.
    [Fact]
    public async Task Patching_to_existing_value_writes_no_trail()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        using var response = await client.PatchAsJsonAsync(
            $"/api/appraisals/{id}",
            new { miles = 74_311, changedBy = "D. Okonjo", reason = "no change" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var trail = await client.GetAsync($"/api/appraisals/{id}/audit");
        trail.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await trail.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    /// A body with only changedBy and reason returns 400; miles with no reason returns 400.
    [Fact]
    public async Task Missing_field_or_reason_both_return_400()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        using var response1 = await client.PatchAsJsonAsync(
            $"/api/appraisals/{id}",
            new { changedBy = "D. Okonjo", reason = "something" });

        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);

        using var doc1 = JsonDocument.Parse(await response1.Content.ReadAsStringAsync());
        Assert.True(doc1.RootElement.TryGetProperty("error", out _));

        using var response2 = await client.PatchAsJsonAsync(
            $"/api/appraisals/{id}",
            new { miles = 74_800, changedBy = "D. Okonjo" });

        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);

        using var doc2 = JsonDocument.Parse(await response2.Content.ReadAsStringAsync());
        Assert.True(doc2.RootElement.TryGetProperty("error", out _));
    }

    /// An unknown appraisal returns 404 on PATCH.
    [Fact]
    public async Task Unknown_appraisal_returns_404_on_patch()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var response = await client.PatchAsJsonAsync(
            "/api/appraisals/9999",
            new { miles = 74_800, changedBy = "D. Okonjo", reason = "test" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
