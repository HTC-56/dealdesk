using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// The lifecycle status transitions and the audit trail they leave — SPEC.md
/// feature 3.  These six assertions prove the POST /status route, the GET
/// /audit route, and the 400 / 409 / 404 boundaries between them.
public sealed class LifecycleApiTests
{
    /// POST /status to `appraised` returns 200 and echoes the new status.
    [Fact]
    public async Task Post_status_to_appraised_returns_200_with_status()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/status",
            new { status = "appraised", changedBy = "D. Okonjo", reason = "walk complete" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("appraised", doc.RootElement.GetProperty("status").GetString());
    }

    /// After one status move the trail has one entry with correct fields.
    [Fact]
    public async Task After_status_move_the_trail_has_one_entry()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/status",
            new { status = "appraised", changedBy = "D. Okonjo", reason = "walk complete" });

        using var response = await client.GetAsync($"/api/appraisals/{id}/audit");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, doc.RootElement.GetArrayLength());
        Assert.Equal("status", doc.RootElement[0].GetProperty("field").GetString());
        Assert.Equal("draft", doc.RootElement[0].GetProperty("oldValue").GetString());
        Assert.Equal("appraised", doc.RootElement[0].GetProperty("newValue").GetString());
    }

    /// Skipping straight from draft to won returns 409.
    [Fact]
    public async Task Draft_to_won_skips_returns_409()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/status",
            new { status = "won", changedBy = "D. Okonjo", reason = "closed deal" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// Unknown status word is 400; missing reason is 400.
    [Fact]
    public async Task Unknown_status_and_missing_reason_both_return_400()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        using var response1 = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/status",
            new { status = "sold", changedBy = "D. Okonjo", reason = "something" });

        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);

        using var doc1 = JsonDocument.Parse(await response1.Content.ReadAsStringAsync());
        Assert.True(doc1.RootElement.TryGetProperty("error", out _));

        using var response2 = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/status",
            new { status = "presented", changedBy = "D. Okonjo" });

        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);

        using var doc2 = JsonDocument.Parse(await response2.Content.ReadAsStringAsync());
        Assert.True(doc2.RootElement.TryGetProperty("error", out _));
    }

    /// The full draft → appraised → presented → won walk succeeds; trail has 3 entries.
    [Fact]
    public async Task Full_walk_succeeds_and_trail_has_three_entries()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await CreateAppraisalAsync(client);

        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/status",
            new { status = "appraised", changedBy = "D. Okonjo", reason = "walk complete" });
        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/status",
            new { status = "presented", changedBy = "D. Okonjo", reason = "offer ready" });
        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/status",
            new { status = "won", changedBy = "D. Okonjo", reason = "buyer accepted" });

        using var response = await client.GetAsync($"/api/appraisals/{id}/audit");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, doc.RootElement.GetArrayLength());
        Assert.Equal("won", doc.RootElement[0].GetProperty("newValue").GetString());
    }

    /// An unknown appraisal returns 404 on both POST and GET.
    [Fact]
    public async Task Unknown_appraisal_returns_404_on_both_verbs()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var posted = await client.PostAsJsonAsync(
            "/api/appraisals/9999/status",
            new { status = "appraised", changedBy = "D. Okonjo", reason = "test" });
        Assert.Equal(HttpStatusCode.NotFound, posted.StatusCode);

        using var listed = await client.GetAsync("/api/appraisals/9999/audit");
        Assert.Equal(HttpStatusCode.NotFound, listed.StatusCode);
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
