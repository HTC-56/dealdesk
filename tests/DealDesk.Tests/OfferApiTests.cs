using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// Offer endpoint API tests — the ones OfferSmokeTests does not cover. These
/// use the HTTP endpoints for comps (§C5) rather than the SeedComp helper,
/// and exercise validation, 404s, and the targetGross subtraction.
public sealed class OfferApiTests
{
    /// A brand-new worksheet's offer-inputs read as zeros and a null anchor.
    [Fact]
    public async Task Get_offer_inputs_on_fresh_worksheet_returns_zeros_and_null_anchor()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.GetAsync($"/api/appraisals/{id}/offer-inputs");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetProperty("pack").GetInt64());
        Assert.Equal(0, doc.RootElement.GetProperty("targetGross").GetInt64());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("anchorOverride").ValueKind);
    }

    /// PUT pack of -1 is refused as a 400 — negative store numbers are meaningless.
    [Fact]
    public async Task Put_offer_inputs_with_negative_pack_returns_400()
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

    /// PUT anchorOverride of 0 is refused — the override must be positive when
    /// present.
    [Fact]
    public async Task Put_offer_inputs_with_zero_anchor_returns_400()
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

    /// An unknown appraisal is a 404 on both offer-inputs and offer.
    [Fact]
    public async Task Offer_routes_on_unknown_appraisal_return_404()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var inputs = await client.PutAsJsonAsync(
            "/api/appraisals/9999/offer-inputs",
            new { pack = 0, targetGross = 0 });
        Assert.Equal(HttpStatusCode.NotFound, inputs.StatusCode);

        using var offer = await client.GetAsync("/api/appraisals/9999/offer");
        Assert.Equal(HttpStatusCode.NotFound, offer.StatusCode);
    }

    /// One comp of 1_000_000, no override: the offer's recommended equals the
    /// comp price and compCount is 1.
    [Fact]
    public async Task Single_comp_produces_recommend_equal_to_comp_price()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/comps",
            new
            {
                label = "demo comparable",
                modelYear = 2019,
                miles = 60_000,
                price = 1_000_000,
            });

        using var saved = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 0 });
        saved.EnsureSuccessStatusCode();

        using var response = await client.GetAsync($"/api/appraisals/{id}/offer");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1_000_000, doc.RootElement.GetProperty("recommended").GetInt64());
        Assert.Equal(1, doc.RootElement.GetProperty("compCount").GetInt32());
    }

    /// Raising targetGross from 0 to 200_000 drops recommended by exactly
    /// 200_000 — the derivation subtracts it.
    [Fact]
    public async Task Raising_targetGross_drops_recommend_by_the_same_amount()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        await client.PostAsJsonAsync(
            $"/api/appraisals/{id}/comps",
            new
            {
                label = "demo comparable",
                modelYear = 2019,
                miles = 60_000,
                price = 1_000_000,
            });

        using var first = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 0 });
        first.EnsureSuccessStatusCode();

        using var firstOffer = await client.GetAsync($"/api/appraisals/{id}/offer");
        firstOffer.EnsureSuccessStatusCode();

        using var firstDoc = JsonDocument.Parse(await firstOffer.Content.ReadAsStringAsync());
        var firstRecommended = firstDoc.RootElement.GetProperty("recommended").GetInt64();

        // Raise targetGross — recommended should drop by exactly 200_000.
        using var second = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 200_000 });
        second.EnsureSuccessStatusCode();

        using var secondOffer = await client.GetAsync($"/api/appraisals/{id}/offer");
        secondOffer.EnsureSuccessStatusCode();

        using var secondDoc = JsonDocument.Parse(await secondOffer.Content.ReadAsStringAsync());
        var secondRecommended = secondDoc.RootElement.GetProperty("recommended").GetInt64();

        Assert.Equal(200_000, firstRecommended - secondRecommended);
    }
}
