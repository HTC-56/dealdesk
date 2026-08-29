using System.Net;
using Xunit;

namespace DealDesk.Tests;

/// The desk page fetches nothing from anywhere — every assertion is a
/// DoesNotContain over the served body so the rule survives edits. Six
/// checks, one factory and client per fact.
///
/// Mirrors DeskPageSmokeTests.cs for structure; reuses PageAsync and
/// DeskAppFactory.
public sealed class DeskPageSelfContainedTests
{
    /// No scheme anywhere in the body — covers every CDN, every font host,
    /// every absolute link in a single assertion.
    [Fact]
    public async Task No_scheme_anywhere_in_the_body()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.DoesNotContain("://", page, StringComparison.Ordinal);
    }

    /// No external script: no <script src> anywhere.
    [Fact]
    public async Task No_external_script_tag()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.DoesNotContain("<script src", page, StringComparison.OrdinalIgnoreCase);
    }

    /// No external stylesheet: no <link> and no @import.
    [Fact]
    public async Task No_external_stylesheet_or_import()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.DoesNotContain("<link ", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@import", page, StringComparison.OrdinalIgnoreCase);
    }

    /// No web font and no image: no @font-face, no <img, no srcset.
    [Fact]
    public async Task No_font_or_image_references()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.DoesNotContain("@font-face", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("srcset", page, StringComparison.OrdinalIgnoreCase);
    }

    /// Exactly one of each inline block: <style>, </style>, <script>,
    /// </script> each appear exactly once.
    [Fact]
    public async Task Exactly_one_inline_block_of_each_kind()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.Equal(1, page.Split("<style>").Length - 1);
        Assert.Equal(1, page.Split("</style>").Length - 1);
        Assert.Equal(1, page.Split("<script>").Length - 1);
        Assert.Equal(1, page.Split("</script>").Length - 1);
    }

    /// The behaviour is truly inline: document.getElementById and fetch(
    /// are present, proving the page is driven by its own script rather
    /// than by markup that would need a fetched framework.
    [Fact]
    public async Task The_page_contains_inline_behaviour()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.Contains("document.getElementById", page, StringComparison.Ordinal);
        Assert.Contains("fetch(", page, StringComparison.Ordinal);
    }
}
