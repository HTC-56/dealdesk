using System.Net;
using System.Text.Json;
using Xunit;

namespace DealDesk.Tests;

/// Six HTTP assertions over a seeded worksheet — list order, status filter,
/// worksheet 1's fields and children, offer inputs, the audit trail. Mirrors
/// `WalkItemApiTests.cs` structure but builds no worksheets; the seed already
/// did. Every fact opens its own factory and client via `SeededClient`.
public sealed class SeedWorksheetApiTests
{
    /// The list endpoint returns all twelve worksheets, newest first.
    [Fact]
    public async Task Get_appraisals_returns_twelve_rows_newest_first()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals");

        Assert.Equal(12, doc.RootElement.GetArrayLength());

        // First row is the newest: id 12.
        Assert.Equal(12L, doc.RootElement[0].GetProperty("id").GetInt64());

        // Last row is the oldest: id 1.
        Assert.Equal(1L, doc.RootElement[11].GetProperty("id").GetInt64());
    }

    /// Status filters narrow the list: won → 4, draft → 2.
    [Fact]
    public async Task Status_filter_narrows_the_appraisal_list()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using (var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals?status=won"))
        {
            Assert.Equal(4, doc.RootElement.GetArrayLength());

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                Assert.Equal("won", row.GetProperty("status").GetString());
            }
        }

        using (var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals?status=draft"))
        {
            Assert.Equal(2, doc.RootElement.GetArrayLength());
        }
    }

    /// Worksheet 1 echoes back its seeded vehicle fields and status.
    [Fact]
    public async Task Get_worksheet_one_returns_the_seeded_vehicle()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals/1");

        Assert.Equal("ZZ9ZZ99Z3Z9000101",
            doc.RootElement.GetProperty("vin").GetString());
        Assert.Equal("Meridian",
            doc.RootElement.GetProperty("make").GetString());
        Assert.Equal("Trailhead",
            doc.RootElement.GetProperty("model").GetString());
        Assert.Equal(74_311,
            doc.RootElement.GetProperty("miles").GetInt32());
        Assert.Equal("A. Whitfield",
            doc.RootElement.GetProperty("appraiser").GetString());
        Assert.Equal("won",
            doc.RootElement.GetProperty("status").GetString());
    }

    /// Worksheet 1's child collections are all populated.
    [Fact]
    public async Task Worksheet_one_child_collections_are_populated()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using (var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals/1/walk-items"))
        {
            Assert.Equal(2, doc.RootElement.GetArrayLength());
        }

        using (var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals/1/recon-lines"))
        {
            Assert.Equal(3, doc.RootElement.GetArrayLength());
        }

        using (var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals/1/comps"))
        {
            Assert.Equal(3, doc.RootElement.GetArrayLength());
        }
    }

    /// Offer inputs exist for priced and unpriced worksheets.
    [Fact]
    public async Task Offer_inputs_for_priced_and_unpriced_worksheets()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using (var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals/1/offer-inputs"))
        {
            Assert.Equal(90_000, doc.RootElement.GetProperty("pack").GetInt64());
            Assert.Equal(150_000,
                doc.RootElement.GetProperty("targetGross").GetInt64());
        }

        // Worksheet 5 is a draft nobody priced — still answers 200,
        // not a 404.
        using (var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals/5/offer-inputs"))
        {
            Assert.Equal(0L, doc.RootElement.GetProperty("pack").GetInt64());
            Assert.Equal(0L,
                doc.RootElement.GetProperty("targetGross").GetInt64());
        }
    }

    /// Worksheet 10's audit trail: four entries, newest first, with
    /// status won and a trim level change.
    [Fact]
    public async Task Audit_trail_for_worksheet_ten_has_four_entries()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        using var doc = await SeedSmokeTests.ReadAsync(
            client, "/api/appraisals/10/audit");

        Assert.Equal(4, doc.RootElement.GetArrayLength());

        // Newest entry: the status move to won.
        Assert.Equal("status",
            doc.RootElement[0].GetProperty("field").GetString());
        Assert.Equal("won",
            doc.RootElement[0].GetProperty("newValue").GetString());

        // Somewhere in the trail: trim level LT → SE.
        var trail = doc.RootElement;
        bool foundTrimChange = false;
        for (int i = 0; i < trail.GetArrayLength(); i++)
        {
            var entry = trail[i];
            var field = entry.GetProperty("field").GetString();
            var oldVal = entry.GetProperty("oldValue").GetString();
            var newVal = entry.GetProperty("newValue").GetString();

            if (field == "trimLevel" && oldVal == "LT" && newVal == "SE")
            {
                foundTrimChange = true;
                break;
            }
        }

        Assert.True(foundTrimChange,
            "No trimLevel LT→SE entry found in worksheet 10's trail");
    }
}
