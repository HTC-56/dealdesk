using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using DealDesk.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DealDesk.Tests;

/// The offer surface end to end: comps and recon lines in, a recommended value
/// and its derivation out.
///
/// Comps and recon lines are seeded straight through Db here rather than over
/// HTTP, so this file exercises the pricing path even before those collections
/// have endpoints of their own.
public sealed class OfferSmokeTests
{
    /// The whole worksheet: two comps average to the anchor, the recon lines
    /// sum, the store numbers come off, and the derivation adds back up to the
    /// number served.
    [Fact]
    public async Task Offer_prices_the_worksheet_and_the_derivation_sums_to_it()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        // $15,000 and $17,000 average to $16,000.
        SeedComp(factory, id, 1_500_000);
        SeedComp(factory, id, 1_700_000);

        // $600 + $400 of recon.
        SeedReconLine(factory, id, 60_000);
        SeedReconLine(factory, id, 40_000);

        using var saved = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 90_000, targetGross = 150_000 });
        saved.EnsureSuccessStatusCode();

        using var response = await client.GetAsync($"/api/appraisals/{id}/offer");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal(1_600_000, root.GetProperty("anchor").GetInt64());
        Assert.Equal(100_000, root.GetProperty("recon").GetInt64());
        Assert.Equal(2, root.GetProperty("compCount").GetInt32());
        Assert.False(root.GetProperty("anchorOverridden").GetBoolean());

        // 1,600,000 − 100,000 − 90,000 − 150,000.
        var recommended = root.GetProperty("recommended").GetInt64();
        Assert.Equal(1_260_000, recommended);

        var derivation = root.GetProperty("derivation");
        Assert.Equal(4, derivation.GetArrayLength());

        long sum = 0;
        foreach (var line in derivation.EnumerateArray())
        {
            sum += line.GetProperty("amount").GetInt64();
            Assert.Equal(sum, line.GetProperty("runningTotal").GetInt64());
        }

        Assert.Equal(recommended, sum);
    }

    /// A typed anchor replaces the comp average outright.
    [Fact]
    public async Task Anchor_override_replaces_the_comp_average()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);
        SeedComp(factory, id, 1_500_000);

        using var saved = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 0, anchorOverride = 1_234_500 });
        saved.EnsureSuccessStatusCode();

        using var response = await client.GetAsync($"/api/appraisals/{id}/offer");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1_234_500, doc.RootElement.GetProperty("anchor").GetInt64());
        Assert.True(doc.RootElement.GetProperty("anchorOverridden").GetBoolean());
        Assert.Equal(1_234_500, doc.RootElement.GetProperty("recommended").GetInt64());
    }

    /// Nothing to price from is a 409: the request is fine, the worksheet is
    /// not ready.
    [Fact]
    public async Task Offer_without_comps_or_override_returns_409()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.GetAsync($"/api/appraisals/{id}/offer");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// The second PUT revises the first — one row per appraisal, not two.
    [Fact]
    public async Task Put_offer_inputs_upserts_rather_than_duplicating()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var id = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var first = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs", new { pack = 50_000, targetGross = 100_000 });
        first.EnsureSuccessStatusCode();

        using var second = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs", new { pack = 75_000, targetGross = 125_000 });
        second.EnsureSuccessStatusCode();

        using var response = await client.GetAsync($"/api/appraisals/{id}/offer-inputs");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(75_000, doc.RootElement.GetProperty("pack").GetInt64());
        Assert.Equal(125_000, doc.RootElement.GetProperty("targetGross").GetInt64());

        using var connection = factory.Services.GetRequiredService<Db>().Open();
        Assert.Equal(
            1,
            connection.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM offer_input WHERE appraisal_id = $id;", new { id }));
    }

    private static void SeedComp(DeskAppFactory factory, long appraisalId, long priceCents)
    {
        using var connection = factory.Services.GetRequiredService<Db>().Open();

        connection.Execute(
            """
            INSERT INTO comp (appraisal_id, label, model_year, miles, price, note, created_at)
            VALUES ($appraisalId, 'comp', 2019, 70000, $priceCents, '', $now);
            """,
            new { appraisalId, priceCents, now = DateTimeOffset.UtcNow.ToString("O") });
    }

    private static void SeedReconLine(DeskAppFactory factory, long appraisalId, long estimateCents)
    {
        using var connection = factory.Services.GetRequiredService<Db>().Open();

        connection.Execute(
            """
            INSERT INTO recon_line (appraisal_id, category, description, estimate, created_at)
            VALUES ($appraisalId, 'mechanical', 'seeded line', $estimateCents, $now);
            """,
            new { appraisalId, estimateCents, now = DateTimeOffset.UtcNow.ToString("O") });
    }
}
