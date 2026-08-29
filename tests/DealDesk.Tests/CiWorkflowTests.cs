using Xunit;

namespace DealDesk.Tests;

/// SPEC.md feature 9, §J8 — CI workflow is what the README promises:
/// nothing but the .NET 8 SDK, no write scope, and a publish that is
/// checked rather than assumed.
public sealed class CiWorkflowTests
{
    /// The GitHub Actions workflow file under test.
    private const string WorkflowFile = PackagingSmokeTests.WorkflowFile;

    /// Read the workflow as plain text — no YAML parser in this repo.
    private static string Workflow => PackagingSmokeTests.ReadRepoFile(WorkflowFile);

    /// §J8 fact 1 — CI runs on both push and pull_request.
    [Fact]
    public void The_workflow_runs_on_push_and_pull_request()
    {
        Assert.Contains("push:", Workflow, StringComparison.Ordinal);
        Assert.Contains("pull_request:", Workflow, StringComparison.Ordinal);
    }

    /// §J8 fact 2 — no write access is requested.
    [Fact]
    public void The_workflow_requests_read_only_permissions()
    {
        Assert.Contains("permissions:", Workflow, StringComparison.Ordinal);
        Assert.Contains("contents: read", Workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("contents: write", Workflow, StringComparison.Ordinal);
    }

    /// §J8 fact 3 — both jobs exist and name the same runner.
    [Fact]
    public void The_workflow_has_both_jobs_with_the_same_runner()
    {
        Assert.Contains("gates:", Workflow, StringComparison.Ordinal);
        Assert.Contains("publish:", Workflow, StringComparison.Ordinal);

        var runnerCount = 0;
        foreach (var line in Workflow.Split('\n'))
        {
            if (line.Contains("runs-on: ubuntu-latest", StringComparison.Ordinal))
                runnerCount++;
        }

        Assert.Equal(2, runnerCount);
    }

    /// §J8 fact 4 — nothing is installed but the SDK.
    [Fact]
    public void The_workflow_installs_only_the_sdk()
    {
        Assert.Contains("actions/checkout@v4", Workflow, StringComparison.Ordinal);
        Assert.Contains("actions/setup-dotnet@v4", Workflow, StringComparison.Ordinal);
        Assert.Contains("8.0.x", Workflow, StringComparison.Ordinal);

        Assert.DoesNotContain("apt-get", Workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("docker", Workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("npm", Workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("pip", Workflow, StringComparison.Ordinal);
    }

    /// §J8 fact 5 — the gates run in the repo's order.
    [Fact]
    public void The_gates_run_in_the_repo_order()
    {
        var buildIndex = Workflow.IndexOf("dotnet build -warnaserror", StringComparison.Ordinal);
        var testIndex = Workflow.IndexOf("dotnet test", StringComparison.Ordinal);
        var formatIndex = Workflow.IndexOf("dotnet format --verify-no-changes", StringComparison.Ordinal);
        var scrubIndex = Workflow.IndexOf("bash scripts/scrub-check.sh", StringComparison.Ordinal);

        Assert.True(buildIndex < testIndex, "build must come before test");
        Assert.True(testIndex < formatIndex, "test must come before format");
        Assert.True(formatIndex < scrubIndex, "format must come before scrub-check");
    }

    /// §J8 fact 6 — the publish is checked, not assumed.
    [Fact]
    public void The_publish_is_checked_not_assumed()
    {
        Assert.Contains("dotnet publish", Workflow, StringComparison.Ordinal);
        Assert.Contains("-c Release", Workflow, StringComparison.Ordinal);
        Assert.Contains("-r linux-x64", Workflow, StringComparison.Ordinal);
        Assert.Contains("find out -type f | wc -l", Workflow, StringComparison.Ordinal);
    }
}
