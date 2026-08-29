using Xunit;

namespace DealDesk.Tests;

/// SPEC.md feature 9 — deploy-grade packaging — publish profile shape. These six
/// facts read the csproj off disk and assert the properties sit inside the
/// RID-conditioned group and that nothing drifted. They mirror
/// PackagingSmokeTests.cs: one [Fact] per behaviour, no HTTP, no client, no async.
public sealed class PublishProfileTests
{
    /// Every publish property is inside the RuntimeIdentifier-conditioned group,
    /// and none of them leaks before it.
    [Fact]
    public void All_five_properties_live_inside_the_RID_conditioned_group()
    {
        var project = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.ProjectFile);

        var conditionIndex = project.IndexOf(
            "Condition=\"'$(RuntimeIdentifier)' != ''\"",
            StringComparison.Ordinal);
        Assert.NotEqual(-1, conditionIndex);

        var firstClosing = project.IndexOf(
            "</PropertyGroup>",
            conditionIndex,
            StringComparison.Ordinal);
        Assert.NotEqual(-1, firstClosing);

        var slice = project.Substring(conditionIndex,
            firstClosing + "</PropertyGroup>".Length - conditionIndex);

        var properties = new[]
        {
            "<SelfContained>true</SelfContained>",
            "<PublishSingleFile>true</PublishSingleFile>",
            "<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>",
            "<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>",
            "<PublishTrimmed>false</PublishTrimmed>",
        };

        foreach (var prop in properties)
        {
            Assert.Contains(prop, slice, StringComparison.Ordinal);
        }

        foreach (var prop in properties)
        {
            var before = project.Substring(0, conditionIndex);
            Assert.DoesNotContain(prop, before, StringComparison.Ordinal);
        }
    }

    /// The project pins no runtime identifier of its own.
    [Fact]
    public void No_runtime_identifier_is_pinned_in_the_project()
    {
        var project = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.ProjectFile);
        Assert.DoesNotContain("<RuntimeIdentifier>", project, StringComparison.Ordinal);
    }

    /// Nothing is trimmed and nothing is AOT — Dapper uses reflection.
    [Fact]
    public void Nothing_is_trimmed_or_AOT()
    {
        var project = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.ProjectFile);

        var conditionIndex = project.IndexOf(
            "Condition=\"'$(RuntimeIdentifier)' != ''\"",
            StringComparison.Ordinal);
        Assert.NotEqual(-1, conditionIndex);

        var firstClosing = project.IndexOf(
            "</PropertyGroup>",
            conditionIndex,
            StringComparison.Ordinal);
        Assert.NotEqual(-1, firstClosing);

        var slice = project.Substring(conditionIndex,
            firstClosing + "</PropertyGroup>".Length - conditionIndex);

        Assert.Contains("<PublishTrimmed>false</PublishTrimmed>", slice, StringComparison.Ordinal);
        Assert.DoesNotContain("<PublishAot>", project, StringComparison.Ordinal);
    }

    /// Debug symbols travel inside the file: DebugType is embedded.
    [Fact]
    public void Debug_symbols_are_embedded_inside_the_file()
    {
        var project = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.ProjectFile);

        var conditionIndex = project.IndexOf(
            "Condition=\"'$(RuntimeIdentifier)' != ''\"",
            StringComparison.Ordinal);
        Assert.NotEqual(-1, conditionIndex);

        var firstClosing = project.IndexOf(
            "</PropertyGroup>",
            conditionIndex,
            StringComparison.Ordinal);
        Assert.NotEqual(-1, firstClosing);

        var slice = project.Substring(conditionIndex,
            firstClosing + "</PropertyGroup>".Length - conditionIndex);

        Assert.Contains("<DebugType>embedded</DebugType>", slice, StringComparison.Ordinal);
    }

    /// The single file carries its own content: both EmbeddedResource includes
    /// with sql. and page. logical-name prefixes.
    [Fact]
    public void Both_embedded_resources_ship_inside_the_binary()
    {
        var project = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.ProjectFile);

        Assert.Contains("sql.%(Filename)%(Extension)", project, StringComparison.Ordinal);
        Assert.Contains("page.%(Filename)%(Extension)", project, StringComparison.Ordinal);
    }

    /// Exactly two PackageReference lines: Dapper and Microsoft.Data.Sqlite.
    [Fact]
    public void Exactly_two_packages_are_referenced()
    {
        var project = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.ProjectFile);

        var lines = project.Split('\n');
        var packageRefLines = lines
            .Where(line => line.Trim().Contains("<PackageReference"))
            .ToArray();

        Assert.Equal(2, packageRefLines.Length);

        var combined = string.Join("\n", packageRefLines);
        Assert.Contains("Dapper", combined, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Data.Sqlite", combined, StringComparison.Ordinal);
    }
}
