using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DealDesk.Data;
using Xunit;

namespace DealDesk.Tests;

/// Six HTTP assertions over POST /api/appraisals and GET /api/appraisals/{id}.
/// Each test gets its own DeskAppFactory so the temp database is isolated.
public sealed class AppraisalApiTests
{
    /// Posting a valid body returns 201 and a Location header.
    [Fact]
    public async Task Post_valid_appraisal_returns_201_and_Location()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var body = new
        {
            vin = "ZZ9ZZ99Z2Z9000042",
            modelYear = 2019,
            make = "Meridian",
            model = "Trailhead",
            trimLevel = "LT",
            miles = 74_311,
            appraiser = "A. Whitfield",
        };

        using var response = await client.PostAsJsonAsync("/api/appraisals", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var location = response.Headers.Location?.ToString();
        Assert.NotNull(location);
        Assert.Contains("/api/appraisals/", location);
    }

    /// GET the row just created — VIN and status match.
    [Fact]
    public async Task Get_appraisal_by_id_returns_200_with_matching_VIN_and_status()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        // Create first.
        var body = new
        {
            vin = "ZZ9ZZ99Z2Z9000042",
            modelYear = 2019,
            make = "Meridian",
            model = "Trailhead",
            trimLevel = "LT",
            miles = 74_311,
            appraiser = "A. Whitfield",
        };

        using var postResp = await client.PostAsJsonAsync("/api/appraisals", body);
        postResp.EnsureSuccessStatusCode();

        var location = postResp.Headers.Location!.ToString();
        // Location is "/api/appraisals/42" — extract the last segment.
        Assert.StartsWith("/api/appraisals/", location);
        var id = location.Split('/').Last();

        // Retrieve.
        using var getResp = await client.GetAsync($"/api/appraisals/{id}");
        getResp.EnsureSuccessStatusCode();

        var json = await getResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal("ZZ9ZZ99Z2Z9000042", doc.RootElement.GetProperty("vin").GetString());
        Assert.Equal("draft", doc.RootElement.GetProperty("status").GetString());
    }

    /// Posting the same body with the known-invalid VIN returns 400.
    [Fact]
    public async Task Post_invalid_VIN_returns_400_with_error_field()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var body = new
        {
            vin = "ZZ9ZZ99Z9Z9000042",
            modelYear = 2019,
            make = "Meridian",
            model = "Trailhead",
            miles = 10_000,
            appraiser = "A. Whitfield",
        };

        using var response = await client.PostAsJsonAsync("/api/appraisals", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// Posting a body with a blank appraiser returns 400.
    [Fact]
    public async Task Post_blank_appraiser_returns_400_with_error_field()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var body = new
        {
            vin = "ZZ9ZZ99Z2Z9000042",
            modelYear = 2019,
            make = "Meridian",
            model = "Trailhead",
            miles = 10_000,
            appraiser = "",
        };

        using var response = await client.PostAsJsonAsync("/api/appraisals", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("error", out _));
    }

    /// GET /api/appraisals returns 200 and an array containing the created row.
    [Fact]
    public async Task List_appraisals_returns_200_with_created_row()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        // Create a row.
        var body = new
        {
            vin = "ZZ9ZZ99Z2Z9000042",
            modelYear = 2019,
            make = "Meridian",
            model = "Trailhead",
            miles = 10_000,
            appraiser = "A. Whitfield",
        };

        await client.PostAsJsonAsync("/api/appraisals", body);

        // List.
        using var response = await client.GetAsync("/api/appraisals");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.True(doc.RootElement.GetArrayLength() >= 1);

        // The newest row (DESC order) should be the one we just created.
        var first = doc.RootElement[0];
        Assert.Equal("ZZ9ZZ99Z2Z9000042", first.GetProperty("vin").GetString());
    }

    /// GET /api/appraisals/9999 returns 404.
    [Fact]
    public async Task Get_nonexistent_appraisal_returns_404()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/appraisals/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
