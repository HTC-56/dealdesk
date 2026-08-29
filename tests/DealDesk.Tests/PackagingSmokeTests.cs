using Xunit;

namespace DealDesk.Tests;

/// SPEC.md feature 9 — deploy-grade packaging — end to end. Its artifacts are
/// files rather than routes: publish properties in a project file, an example
/// systemd unit, an environment file and a CI workflow. None of them ships any
/// code, so nothing copies them into a test output directory and there is no
/// HTTP client that can ask them anything. These tests read them off disk.
///
/// That is worth the cost, because these four files are the ones a reader of
/// the work sample will never run and will absolutely read. A unit file whose
/// ExecStart names a binary the publish does not emit, or an environment file
/// naming a key the app ignores, is documentation that lies — and lies quietly,
/// because every gate in the repo would still be green.
///
/// These four facts are the headline; the later packaging test files mirror
/// this one and reuse its helpers.
public sealed class PackagingSmokeTests
{
    /// The project file the publish properties live in.
    internal const string ProjectFile = "src/DealDesk/DealDesk.csproj";

    /// The example systemd unit, and the environment file it reads.
    internal const string UnitFile = "deploy/dealdesk.service";
    internal const string EnvExample = "deploy/dealdesk.env.example";

    /// The GitHub Actions workflow that runs the gates on every push.
    internal const string WorkflowFile = ".github/workflows/ci.yml";

    /// Where the configuration keys are read, and the prefix they are read
    /// under. `DEALDESK_Db__Path` in the environment is the key `Db:Path` here.
    internal const string ProgramFile = "src/DealDesk/Program.cs";
    internal const string EnvPrefix = "DEALDESK_";

    /// The name `dotnet publish` gives the single file — the project's
    /// AssemblyName. The unit's ExecStart has to spell it exactly.
    internal const string PublishedBinary = "DealDesk";

    /// The repo root, found by walking up from the test assembly until the
    /// solution file appears. Computed rather than written down, because an
    /// absolute path in a tracked file is what scrub-check.sh exists to refuse.
    internal static string RepoRoot { get; } = FindRepoRoot();

    /// One repo file as text, with LF line endings whatever the checkout did.
    internal static string ReadRepoFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepoRoot, relativePath))
            .Replace("\r\n", "\n", StringComparison.Ordinal);

    /// One repo file as its settings: non-blank lines with the comments and the
    /// indentation taken off. `#` opens a comment in a systemd unit, in an
    /// environment file and in a YAML workflow alike.
    internal static string[] SettingLines(string relativePath) =>
        ReadRepoFile(relativePath)
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

    /// The configuration keys an environment file sets, in the spelling
    /// Program.cs asks for them by: the `DEALDESK_` prefix comes off and a
    /// double underscore becomes a colon, so `DEALDESK_Ops__LedgerPath` reads
    /// back as `Ops:LedgerPath`. Lines for other variables are left out.
    internal static string[] ConfigurationKeys(string relativePath) =>
        SettingLines(relativePath)
            .Where(line => line.StartsWith(EnvPrefix, StringComparison.Ordinal))
            .Select(line => line[EnvPrefix.Length..].Split('=')[0])
            .Select(name => name.Replace("__", ":", StringComparison.Ordinal))
            .ToArray();

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DealDesk.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "DealDesk.sln was not found above " + AppContext.BaseDirectory);
    }

    /// The headline of feature 9: one command yields one self-contained file.
    /// Every property is conditioned on a runtime identifier being chosen, so
    /// the three gates that run without one are untouched by any of it.
    [Fact]
    public void The_project_publishes_one_self_contained_file()
    {
        var project = ReadRepoFile(ProjectFile);

        Assert.Contains("Condition=\"'$(RuntimeIdentifier)' != ''\"", project, StringComparison.Ordinal);
        Assert.Contains("<SelfContained>true</SelfContained>", project, StringComparison.Ordinal);
        Assert.Contains("<PublishSingleFile>true</PublishSingleFile>", project, StringComparison.Ordinal);
        Assert.Contains(
            "<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>",
            project,
            StringComparison.Ordinal);

        // Dapper materialises rows by reflection; a trimmed publish would build
        // clean and fail at the first query.
        Assert.Contains("<PublishTrimmed>false</PublishTrimmed>", project, StringComparison.Ordinal);
    }

    /// The unit starts the file the publish actually emits, and keeps the
    /// binary and the data apart.
    [Fact]
    public void The_unit_starts_the_published_binary()
    {
        var unit = SettingLines(UnitFile);

        Assert.Contains("[Unit]", unit);
        Assert.Contains("[Service]", unit);
        Assert.Contains("[Install]", unit);

        var execStart = Assert.Single(unit.Where(line => line.StartsWith("ExecStart=", StringComparison.Ordinal)));
        Assert.EndsWith("/" + PublishedBinary, execStart, StringComparison.Ordinal);

        Assert.Contains("EnvironmentFile=/etc/dealdesk/dealdesk.env", unit);
        Assert.Contains("WorkingDirectory=/var/lib/dealdesk", unit);
    }

    /// Every key the environment file sets is a key Program.cs reads. An
    /// example that configures something the app ignores would be worse than
    /// no example at all.
    [Fact]
    public void The_environment_example_names_only_keys_the_app_reads()
    {
        var program = ReadRepoFile(ProgramFile);
        var keys = ConfigurationKeys(EnvExample);

        Assert.NotEmpty(keys);

        foreach (var key in keys)
        {
            Assert.Contains("\"" + key + "\"", program, StringComparison.Ordinal);
        }

        Assert.Contains("AddEnvironmentVariables(\"" + EnvPrefix + "\")", program, StringComparison.Ordinal);
    }

    /// CI runs the gates the repo says green means, rather than a shorter list
    /// that would let a red tree merge.
    [Fact]
    public void The_workflow_runs_every_gate()
    {
        var workflow = ReadRepoFile(WorkflowFile);

        Assert.Contains("dotnet build -warnaserror", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet test", workflow, StringComparison.Ordinal);
        Assert.Contains("dotnet format --verify-no-changes", workflow, StringComparison.Ordinal);
        Assert.Contains("bash scripts/scrub-check.sh", workflow, StringComparison.Ordinal);
    }
}
