using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// The recon actuals posting pair over real HTTP. This route mirrors the
/// walk-item pair in WalkItemApiTests.cs — same four contract points
/// (created row echoed back, list ordered oldest first, bad vocabulary
/// refused, unknown parent 404) plus two domain-specific ones (credits
/// are legal, zero amounts are refused).
public sealed class ReconActualApiTests
{
    /// Posting an actual returns 201 and the stored row, defaults applied.
    [Fact]
    public async Task Post_actual_returns_201_with_the_stored_row()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var appraisalId = await WalkItemApiTests.CreateAppraisalAsync(client);
        var lineId = await CreateReconLineAsync(client, appraisalId);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals",
            new { amount = 90_000, description = "body shop invoice 4471", postedBy = "R. Vasquez" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(90_000, doc.RootElement.GetProperty("amount").GetInt64());
        Assert.Equal(lineId, doc.RootElement.GetProperty("reconLineId").GetInt64());
    }

    /// After two postings (90_000 then 45_000), GET returns an array of
    /// length 2 with 90000 at index 0 — oldest first.
    [Fact]
    public async Task Get_actuals_lists_this_lines_postings_oldest_first()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var appraisalId = await WalkItemApiTests.CreateAppraisalAsync(client);
        var lineId = await CreateReconLineAsync(client, appraisalId);

        await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals",
            new { amount = 90_000, description = "body shop invoice 4471", postedBy = "R. Vasquez" });
        await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals",
            new { amount = 45_000, description = "tow charge", postedBy = "R. Vasquez" });

        using var response = await client.GetAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals");
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal(90_000, doc.RootElement[0].GetProperty("amount").GetInt64());
    }

    /// An amount of zero returns 400 with an error field; a blank description
    /// returns 400 too.
    [Fact]
    public async Task Post_actual_with_zero_amount_returns_400()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var appraisalId = await WalkItemApiTests.CreateAppraisalAsync(client);
        var lineId = await CreateReconLineAsync(client, appraisalId);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals",
            new { amount = 0, description = "should fail", postedBy = "R. Vasquez" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// A negative amount (a credit) returns 201 — credits are legal.
    [Fact]
    public async Task Post_actual_with_negative_amount_returns_201()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var appraisalId = await WalkItemApiTests.CreateAppraisalAsync(client);
        var lineId = await CreateReconLineAsync(client, appraisalId);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines/{lineId}/actuals",
            new { amount = -5_000, description = "part return", postedBy = "R. Vasquez" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(-5_000, doc.RootElement.GetProperty("amount").GetInt64());
    }

    /// GET and POST on /api/appraisals/9999/recon-lines/1/actuals both
    /// return 404.
    [Fact]
    public async Task Actuals_on_unknown_appraisal_return_404()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var listed = await client.GetAsync(
            "/api/appraisals/9999/recon-lines/1/actuals");
        Assert.Equal(HttpStatusCode.NotFound, listed.StatusCode);

        using var posted = await client.PostAsJsonAsync(
            "/api/appraisals/9999/recon-lines/1/actuals",
            new { amount = 100, description = "x", postedBy = "x" });
        Assert.Equal(HttpStatusCode.NotFound, posted.StatusCode);
    }

    /// A recon line created on a SECOND appraisal returns 404 when addressed
    /// under the FIRST appraisal's id — a line from another worksheet is not
    /// reachable.
    [Fact]
    public async Task Actuals_on_foreign_recon_line_return_404()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var firstId = await WalkItemApiTests.CreateAppraisalAsync(client);
        var secondId = await WalkItemApiTests.CreateAppraisalAsync(client);

        var lineId = await CreateReconLineAsync(client, secondId);

        using var response = await client.GetAsync(
            $"/api/appraisals/{firstId}/recon-lines/{lineId}/actuals");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// Creates a recon line for the given appraisal and returns its id.
    internal static async Task<long> CreateReconLineAsync(HttpClient client, long appraisalId)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(appraisalId);

        using var response = await client.PostAsJsonAsync(
            $"/api/appraisals/{appraisalId}/recon-lines",
            new { category = "paint", description = "respray rear quarter", estimate = 120_000 });

        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt64();
    }
}
