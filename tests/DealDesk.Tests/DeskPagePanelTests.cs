using DealDesk.Api;
using DealDesk.Domain;
using Xunit;

namespace DealDesk.Tests;

/// The four panels on the desk page carry the controls and tables the spec
/// demands — panel order, status filter, recon categories, offer derivation,
/// audit columns, and report tables. Mirror `DeskPageSmokeTests.cs`.
public sealed class DeskPagePanelTests
{
    /// The panels appear in reading order: appraisals → worksheet → audit →
    /// reports. No panel is missing (no −1).
    [Fact]
    public async Task Panels_are_in_specified_reading_order()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        var appraisals = page.IndexOf("<section id=\"appraisals\">", StringComparison.Ordinal);
        var worksheet = page.IndexOf("<section id=\"worksheet\">", StringComparison.Ordinal);
        var audit = page.IndexOf("<section id=\"audit\">", StringComparison.Ordinal);
        var reports = page.IndexOf("<section id=\"reports\">", StringComparison.Ordinal);

        Assert.True(appraisals > -1, "appraisals panel missing");
        Assert.True(worksheet > -1, "worksheet panel missing");
        Assert.True(audit > -1, "audit panel missing");
        Assert.True(reports > -1, "reports panel missing");

        Assert.True(appraisals < worksheet, "appraisals must come before worksheet");
        Assert.True(worksheet < audit, "worksheet must come before audit");
        Assert.True(audit < reports, "audit must come before reports");
    }

    /// Every lifecycle status is offered as a filter option so the list
    /// panel can show every state.
    [Fact]
    public async Task Every_lifecycle_status_appears_as_a_filter_option()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        foreach (var status in Lifecycle.All)
        {
            Assert.Contains("<option value=\"" + status + "\">", page, StringComparison.Ordinal);
        }
    }

    /// Every recon category is typeable via a filter option.
    [Fact]
    public async Task Every_recon_category_appears_as_a_filter_option()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        foreach (var category in Vocabulary.ReconCategories)
        {
            Assert.Contains("<option>" + category + "</option>", page, StringComparison.Ordinal);
        }
    }

    /// The offer math section carries the derivation table and the four
    /// step labels.
    [Fact]
    public async Task The_derivation_table_and_labels_are_on_the_page()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.Contains("<tbody id=\"derivation\">", page, StringComparison.Ordinal);
        Assert.Contains("<strong id=\"recommended\">", page, StringComparison.Ordinal);
        Assert.Contains("Market anchor (comp average)", page, StringComparison.Ordinal);
        Assert.Contains("Less recon estimate", page, StringComparison.Ordinal);
        Assert.Contains("Less pack", page, StringComparison.Ordinal);
        Assert.Contains("Less target front gross", page, StringComparison.Ordinal);
    }

    /// The audit panel names the trail's columns.
    [Fact]
    public async Task The_audit_panel_names_the_trail_columns()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.Contains("<th>Field</th>", page, StringComparison.Ordinal);
        Assert.Contains("<th>From</th>", page, StringComparison.Ordinal);
        Assert.Contains("<th>To</th>", page, StringComparison.Ordinal);
        Assert.Contains("<th>By</th>", page, StringComparison.Ordinal);
        Assert.Contains("<th>Reason</th>", page, StringComparison.Ordinal);
    }

    /// All three report tables are present on the page.
    [Fact]
    public async Task All_three_report_tables_are_on_the_page()
    {
        using var factory = new DeskAppFactory();
        using var client = factory.CreateClient();

        var page = await DeskPageSmokeTests.PageAsync(client);

        Assert.Contains("<table id=\"look-to-book\">", page, StringComparison.Ordinal);
        Assert.Contains("<table id=\"recon-variance\">", page, StringComparison.Ordinal);
        Assert.Contains("<table id=\"front-gross\">", page, StringComparison.Ordinal);
    }
}
