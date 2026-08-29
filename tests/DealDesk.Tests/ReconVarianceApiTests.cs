using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// The recon variance report over real HTTP. It mirrors
/// ReconActualApiTests.cs — same factory, same appraisal helper, same
/// JsonDocument reads — and reuses CreateReconLineAsync (with category
/// and estimate parameters) so a test can make a tires line too.
///
/// GET .../recon-variance always returns 200 for a real appraisal; there
/// is no 409 here because an empty sum is genuinely zero, not a refusal.
public sealed class ReconVarianceApiTests
{
    /// An appraisal with no recon lines returns 200 with totalEstimate 0,
    /// totalVariance 0, and a lines array of length 0.
    [Fact]
    public async Task Empty_worksheet_returns_zero_totals_and_no_lines()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var appraisalId = await WalkItemApiTests.CreateAppraisalAsync(client);

        using var response = await client.GetAsync(
            $"/api/appraisals/{appraisalId}/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetProperty("totalEstimate").GetInt64());
        Assert.Equal(0, doc.RootElement.GetProperty("totalVariance").GetInt64());
        Assert.Equal(0, doc.RootElement.GetProperty("lines").GetArrayLength());
    }

    /// Two lines and no postings: totalEstimate is their sum, totalActual
    /// is 0, unpostedLines is 2, and lines[0].posted is false.
    [Fact]
    public async Task Two_lines_no_postings_show_unposted()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var appraisalId = await WalkItemApiTests.CreateAppraisalAsync(client);

        await CreateReconLineAsync(client, appraisalId, "paint", 120_000);
        await CreateReconLineAsync(client, appraisalId, "tires", 80_000);

        using var response = await client.GetAsync(
            $"/api/appraisals/{appraisalId}/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(200_000, doc.RootElement.GetProperty("totalEstimate").GetInt64());
        Assert.Equal(0, doc.RootElement.GetProperty("totalActual").GetInt64());
        Assert.Equal(2, doc.RootElement.GetProperty("unpostedLines").GetInt32());
        Assert.False(doc.RootElement.GetProperty("lines")[0].GetProperty("posted").GetBoolean());
    }

    /// After posting 90_000 and 45_000 against a 120_000 paint line, that
    /// line's actual is 135000, variance is 15000 and postingCount is 2.
    [Fact]
    public async Task Posted_lines_show_correct_actual_variance_and_count()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var appraisalId = await WalkItemApiTests.CreateAppraisalAsync(client);
        var lineId = await CreateReconLineAsync(client, appraisalId, "paint", 120_000);

        await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals",
            new { amount = 90_000, description = "body shop invoice 4471", postedBy = "R. Vasquez" });
        await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals",
            new { amount = 45_000, description = "tow charge", postedBy = "R. Vasquez" });

        using var response = await client.GetAsync(
            $"/api/appraisals/{appraisalId}/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var line = doc.RootElement.GetProperty("lines")[0];
        Assert.Equal(135_000, line.GetProperty("actual").GetInt64());
        Assert.Equal(15_000, line.GetProperty("variance").GetInt64());
        Assert.Equal(2, line.GetProperty("postingCount").GetInt32());
    }

    /// On the same worksheet, totalVariance equals totalActual minus
    /// totalEstimate.
    [Fact]
    public async Task TotalVariance_equals_totalActual_minus_totalEstimate()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var appraisalId = await WalkItemApiTests.CreateAppraisalAsync(client);
        var lineId = await CreateReconLineAsync(client, appraisalId, "paint", 120_000);

        await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals",
            new { amount = 90_000, description = "body shop invoice 4471", postedBy = "R. Vasquez" });

        using var response = await client.GetAsync(
            $"/api/appraisals/{appraisalId}/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var totalEstimate = doc.RootElement.GetProperty("totalEstimate").GetInt64();
        var totalActual = doc.RootElement.GetProperty("totalActual").GetInt64();
        var totalVariance = doc.RootElement.GetProperty("totalVariance").GetInt64();

        Assert.Equal(totalActual - totalEstimate, totalVariance);
    }

    /// Two paint lines and one tires line produce a byCategory array of
    /// length 2, with byCategory[0].category "paint" and its lineCount 2.
    [Fact]
    public async Task ByCategory_groups_lines_correctly()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var appraisalId = await WalkItemApiTests.CreateAppraisalAsync(client);

        await CreateReconLineAsync(client, appraisalId, "paint", 120_000);
        await CreateReconLineAsync(client, appraisalId, "paint", 95_000);
        await CreateReconLineAsync(client, appraisalId, "tires", 80_000);

        using var response = await client.GetAsync(
            $"/api/appraisals/{appraisalId}/recon-variance");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var byCategory = doc.RootElement.GetProperty("byCategory");
        Assert.Equal(2, byCategory.GetArrayLength());
        Assert.Equal("paint", byCategory[0].GetProperty("category").GetString());
        Assert.Equal(2, byCategory[0].GetProperty("lineCount").GetInt32());
    }

    /// GET /api/appraisals/9999/recon-variance returns 404.
    [Fact]
    public async Task Recon_variance_on_unknown_appraisal_returns_404()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/appraisals/9999/recon-variance");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// Creates a recon line for the given appraisal with the specified
    /// category and estimate, and returns its id.
    internal static async Task<long> CreateReconLineAsync(
        HttpClient client, long appraisalId, string category, long estimate)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(appraisalId);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines",
            new { category, description = "respray rear quarter", estimate });

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt64();
    }
}
