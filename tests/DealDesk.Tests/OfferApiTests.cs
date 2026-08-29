using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using DealDesk.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DealDesk.Tests;

/// HTTP assertions over the offer-inputs and offer endpoints that
/// OfferSmokeTests.cs does not already cover.
///
/// Comps and recon lines are posted over HTTP (no SeedComp helper).
public sealed class OfferApiTests
{
    /// GET /offer-inputs on a brand-new appraisal returns pack 0,
    /// targetGross 0, and anchorOverride null.
    [Fact]
    public async Task Offer_inputs_on_a_fresh_worksheet_returns_zeros()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.GetAsync(
            $"/api/appraisals/{id}/offer-inputs");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(0, root.GetProperty("pack").GetInt64());
        Assert.Equal(0, root.GetProperty("targetGross").GetInt64());
        Assert.True(
            root.GetProperty("anchorOverride").ValueKind == JsonValueKind.Null);
    }

    /// PUT /offer-inputs with pack of -1 returns 400 with an error field.
    [Fact]
    public async Task Negative_pack_returns_400()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = -1, targetGross = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// PUT /offer-inputs with anchorOverride of 0 returns 400 — the
    /// override must be positive when present.
    [Fact]
    public async Task Anchor_override_of_zero_returns_400()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 0, anchorOverride = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// PUT /offer-inputs and GET /offer on a nonexistent appraisal
    /// both return 404.
    [Fact]
    public async Task Offer_on_nonexistent_appraisal_returns_404()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var put = await client.PutAsJsonAsync(
            "/api/appraisals/9999/offer-inputs",
            new { pack = 0, targetGross = 0 });
        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);

        using var get = await client.GetAsync("/api/appraisals/9999/offer");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    /// With one comp of 1 000 000 and no pack or target gross, the
    /// recommended price equals that comp price and compCount is 1.
    [Fact]
    public async Task Single_comp_becomes_the_recommended_price()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        // Seed one comp over HTTP.
        using var compPost = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/comps",
            new
            {
                label = "bluebook",
                modelYear = 2020,
                miles = 50000,
                price = 1_000_000,
                note = "",
            });
        compPost.EnsureSuccessStatusCode();

        // Zero pack, zero target gross — nothing to subtract.
        using var inputs = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 0 });
        inputs.EnsureSuccessStatusCode();

        using var response = await client.GetAsync($"/api/appraisals/{id}/offer");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(1_000_000, root.GetProperty("recommended").GetInt64());
        Assert.Equal(1, root.GetProperty("compCount").GetInt32());
    }

    /// Raising targetGross from 0 to 200 000 drops recommended by
    /// exactly 200 000.
    [Fact]
    public async Task Raising_target_gross_drops_recommended_by_the_same_amount()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        // Seed one comp of 1 000 000.
        using var compPost = await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/comps",
            new
            {
                label = "bluebook",
                modelYear = 2020,
                miles = 50000,
                price = 1_000_000,
                note = "",
            });
        compPost.EnsureSuccessStatusCode();

        // Start with zero pack, zero target gross.
        using var inputs0 = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 0 });
        inputs0.EnsureSuccessStatusCode();

        using var response0 = await client.GetAsync($"/api/appraisals/{id}/offer");
        response0.EnsureSuccessStatusCode();

        using var doc0 = JsonDocument.Parse(await response0.Content.ReadAsStringAsync());
        var recommended0 = doc0.RootElement
            .GetProperty("recommended").GetInt64();

        // Raise targetGross to 200 000.
        using var inputs1 = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 200_000 });
        inputs1.EnsureSuccessStatusCode();

        using var response1 = await client.GetAsync($"/api/appraisals/{id}/offer");
        response1.EnsureSuccessStatusCode();

        using var doc1 = JsonDocument.Parse(await response1.Content.ReadAsStringAsync());
        var recommended1 = doc1.RootElement
            .GetProperty("recommended").GetInt64();

        Assert.Equal(200_000, recommended0 - recommended1);
    }
}
