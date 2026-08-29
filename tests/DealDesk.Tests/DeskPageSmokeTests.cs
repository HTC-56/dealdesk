using System.Net;
using Xunit;

namespace DealDesk.Tests;

/// The desk page of SPEC.md feature 6, end to end: one document at `/`, no
/// request to anywhere else, the four panels the spec names, and an API
/// underneath that answers everything the page asks it.
///
/// The page is a work sample, so its self-containment is a contract rather than
/// a style choice — a stylesheet, a font or a script pulled from somewhere else
/// would break the "zero outbound requests" rule in the one place a reviewer
/// actually looks. These four facts are the headline; the later desk-page test
/// files mirror this one and reuse its helpers.
public sealed class DeskPageSmokeTests
{
    /// The desk page is the only route in the repo that answers the root.
    internal const string PageRoute = "/";

    /// SPEC.md feature 6 names four panels: the worksheet form with its live
    /// offer math, the appraisal list with lifecycle states, the audit trail,
    /// and the three reports. Each is a `<section id="…">` on the page.
    internal static readonly string[] PanelIds =
        ["appraisals", "worksheet", "audit", "reports"];

    /// Every route the page's script GETs, with the seeded ids it reaches them
    /// by. A page that names a route the service does not serve is a broken
    /// page that still passes a markup test, so the desk-page tests probe
    /// these for real.
    internal static readonly string[] ReadRoutes =
    [
        "/api/appraisals",
        "/api/appraisals/1",
        "/api/appraisals/1/walk-items",
        "/api/appraisals/1/recon-lines",
        "/api/appraisals/1/comps",
        "/api/appraisals/1/offer-inputs",
        "/api/appraisals/1/offer",
        "/api/appraisals/1/recon-variance",
        "/api/appraisals/1/audit",
        "/api/reports/look-to-book",
        "/api/reports/recon-variance",
        "/api/reports/front-gross",
    ];

    /// One response carries the whole page — markup, styling and behaviour.
    [Fact]
    public async Task The_root_serves_one_complete_html_document()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(PageRoute);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var page = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("<!doctype html>", page, StringComparison.Ordinal);
        Assert.Contains("<title>dealdesk", page, StringComparison.Ordinal);
        Assert.EndsWith("</html>", page.TrimEnd(), StringComparison.Ordinal);
    }

    /// The whole promise of the page in one assertion: there is no scheme
    /// anywhere in it, so a browser that loads it opens no second connection.
    [Fact]
    public async Task The_page_fetches_nothing_from_anywhere()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await PageAsync(client);

        Assert.DoesNotContain("://", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<script src", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<link ", page, StringComparison.OrdinalIgnoreCase);
    }

    /// Feature 6's four panels, present as sections a reader can link to.
    [Fact]
    public async Task The_page_carries_the_four_panels_the_spec_names()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await PageAsync(client);

        foreach (var id in PanelIds)
        {
            Assert.Contains("<section id=\"" + id + "\">", page, StringComparison.Ordinal);
        }
    }

    /// The page is only as good as the API under it, so the routes it calls are
    /// answered against the demo month rather than assumed.
    [Fact]
    public async Task Every_route_the_page_reads_answers_over_the_seeded_month()
    {
        using var factory = new DeskAppFactory();
        using var client = SeedSmokeTests.SeededClient(factory);

        foreach (var route in ReadRoutes)
        {
            using var response = await client.GetAsync(route);
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                route + " answered " + (int)response.StatusCode);
        }
    }

    /// GETs the desk page and hands back its body. The page must always be
    /// there — a 404 here is a broken build, not a test case.
    internal static async Task<string> PageAsync(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        using var response = await client.GetAsync(PageRoute);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
