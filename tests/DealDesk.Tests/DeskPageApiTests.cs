using System.Net.Http.Json;
using Xunit;

namespace DealDesk.Tests;

/// Six facts about the root route — content type, caching, 404s, open under
/// the token, counted in /metrics, not truncated.
///
/// Each test gets its own factory, so no test sees another's counts. Mirrors
/// MetricsApiTests.cs.
public sealed class DeskPageApiTests
{
    /// The content type names HTML and UTF-8.
    [Fact]
    public async Task The_content_type_names_HTML_and_UTF8()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative));
        response.EnsureSuccessStatusCode();

        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
    }

    /// Two GETs of / return byte-identical bodies.
    [Fact]
    public async Task Two_GETs_of_root_return_byte_identical_bodies()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var first = await DeskPageSmokeTests.PageAsync(client);
        var second = await DeskPageSmokeTests.PageAsync(client);

        Assert.Equal(first, second);
    }

    /// The page is not a catch-all: /desk, /index.html, /api/nope are 404.
    [Fact]
    public async Task Only_the_exact_root_is_the_page()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var desk = await client.GetAsync(new Uri("/desk", UriKind.Relative));
        var index = await client.GetAsync(new Uri("/index.html", UriKind.Relative));
        var apiNope = await client.GetAsync(new Uri("/api/nope", UriKind.Relative));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, desk.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, index.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, apiNope.StatusCode);
    }

    /// The page is open when the token is armed.
    [Fact]
    public async Task The_page_is_open_under_the_token()
    {
        using var factory = new DeskAppFactory(OpsSmokeTests.DemoToken);
        using var client = factory.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative));
        response.EnsureSuccessStatusCode();
    }

    /// The page is counted like any other request in /metrics.
    [Fact]
    public async Task The_page_is_counted_in_metrics()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        await client.GetAsync(new Uri("/", UriKind.Relative));

        var text = await OpsSmokeTests.ScrapeUntilAsync(
            client,
            scrape => scrape.Contains(
                "dealdesk_http_requests_total{method=\"GET\",status=\"200\"} 1",
                StringComparison.Ordinal));

        Assert.Contains(
            "dealdesk_http_requests_total{method=\"GET\",status=\"200\"} 1",
            text,
            StringComparison.Ordinal);
    }

    /// The page is not empty and not truncated.
    [Fact]
    public async Task The_page_is_not_empty_and_not_truncated()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.True(page.Length > 10_000, $"Page length {page.Length} is not over 10 000");
        Assert.Contains("</style>", page);
        Assert.Contains("</script>", page);
    }
}
