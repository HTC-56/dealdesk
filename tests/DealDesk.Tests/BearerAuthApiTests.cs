using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// The bearer token guard over HTTP — SPEC.md feature 7, §G8.
///
/// Six assertions prove the guard: no token and wrong token are 401, writes
/// are guarded while reads stay open, near-miss values still fail, and an
/// unset token leaves everything open.
public sealed class BearerAuthApiTests
{
    /// POST /api/appraisals with no Authorization header is 401 and the JSON
    /// body's error is a non-empty string.
    [Fact]
    public async Task No_authorization_header_returns_401_with_error()
    {
        using var factory = new DeskAppFactory(OpsSmokeTests.DemoToken);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/appraisals",
            OpsSmokeTests.NewWorksheet());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("error", out var error));
        Assert.NotEqual(string.Empty, error.GetString());
    }

    /// The same POST carrying the token "not-the-token" is 401 too — a wrong
    /// credential is refused exactly like a missing one.
    [Fact]
    public async Task Wrong_token_returns_401()
    {
        using var factory = new DeskAppFactory(OpsSmokeTests.DemoToken);
        using var client = factory.CreateClient();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not-the-token");

        using var response = await client.PostAsJsonAsync(
            "/api/appraisals",
            OpsSmokeTests.NewWorksheet());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// The guard is not POST-only. With the token attached, create a
    /// worksheet (201); then clear the header and PUT /offer-inputs — 401.
    /// Re-attach the token and the same PUT succeeds.
    [Fact]
    public async Task Put_is_guarded_and_succeeds_with_token()
    {
        using var factory = new DeskAppFactory(OpsSmokeTests.DemoToken);
        using var client = factory.CreateClient();

        // Create a worksheet with the token.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", OpsSmokeTests.DemoToken);

        using var created = await client.PostAsJsonAsync(
            "/api/appraisals",
            OpsSmokeTests.NewWorksheet());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetInt64();

        // Clear the header and PUT — should be 401.
        client.DefaultRequestHeaders.Authorization = null;

        using var refused = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 150_000 });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // Re-attach the token and the PUT succeeds.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", OpsSmokeTests.DemoToken);

        using var accepted = await client.PutAsJsonAsync(
            $"/api/appraisals/{id}/offer-inputs",
            new { pack = 0, targetGross = 150_000 });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    /// Reads stay open. PATCH without a header is 401, GET without a header
    /// is 200.
    [Fact]
    public async Task Reads_are_open_while_patches_require_token()
    {
        using var factory = new DeskAppFactory(OpsSmokeTests.DemoToken);
        using var client = factory.CreateClient();

        // Create a worksheet with the token so we have something to PATCH/GET.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", OpsSmokeTests.DemoToken);

        using var created = await client.PostAsJsonAsync(
            "/api/appraisals",
            OpsSmokeTests.NewWorksheet());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetInt64();

        // Clear the header.
        client.DefaultRequestHeaders.Authorization = null;

        // PATCH without token is 401.
        using var patched = await client.PatchAsJsonAsync(
            $"/api/appraisals/{id}",
            new { miles = 80_000, changedBy = "D. Okonjo", reason = "odometer" });
        Assert.Equal(HttpStatusCode.Unauthorized, patched.StatusCode);

        // GET without token is 200.
        using var fetched = await client.GetAsync($"/api/appraisals/{id}");
        fetched.EnsureSuccessStatusCode();
    }

    /// A near miss is still a miss: the token "desk-demo-token-extra" is 401
    /// even though it starts with the right value. So is the right value sent
    /// under the wrong scheme — Basic instead of Bearer.
    [Fact]
    public async Task Near_miss_and_wrong_scheme_are_both_401()
    {
        using var factory = new DeskAppFactory(OpsSmokeTests.DemoToken);
        using var client = factory.CreateClient();

        // Near miss token.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "desk-demo-token-extra");

        using var nearMiss = await client.PostAsJsonAsync(
            "/api/appraisals",
            OpsSmokeTests.NewWorksheet());
        Assert.Equal(HttpStatusCode.Unauthorized, nearMiss.StatusCode);

        // Right value, wrong scheme.
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", OpsSmokeTests.DemoToken);

        using var wrongScheme = await client.PostAsJsonAsync(
            "/api/appraisals",
            OpsSmokeTests.NewWorksheet());
        Assert.Equal(HttpStatusCode.Unauthorized, wrongScheme.StatusCode);
    }

    /// An unset token leaves writes open: with DeskAppFactory() and no header
    /// at all, POST /api/appraisals answers 201.
    [Fact]
    public async Task Unset_token_leaves_writes_open()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/appraisals",
            OpsSmokeTests.NewWorksheet());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
