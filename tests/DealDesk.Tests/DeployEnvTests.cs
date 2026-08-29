using Xunit;

namespace DealDesk.Tests;

/// SPEC.md feature 9 — deploy-grade packaging — the environment example file.
///
/// The smoke test asserts every key the example sets is a key Program.cs reads.
/// This file asserts the rest of the contract: the shape of the lines, what is
/// deliberately left unset, and where the paths point.
public sealed class DeployEnvTests
{
    /// Every key the environment file sets is a key Program.cs reads. An
    /// example that configures something the app ignores would be worse than
    /// no example at all.
    [Fact]
    public void Every_setting_is_a_clean_assignment()
    {
        var lines = PackagingSmokeTests.SettingLines(PackagingSmokeTests.EnvExample);

        foreach (var line in lines)
        {
            var eqIndex = line.IndexOf('=', StringComparison.Ordinal);
            Assert.NotEqual(-1, eqIndex);

            var name = line.Substring(0, eqIndex);
            Assert.NotEmpty(name);
            Assert.False(name.Contains(' ', StringComparison.Ordinal));
        }
    }

    [Fact]
    public void The_two_live_keys_match_program_cs()
    {
        var keys = PackagingSmokeTests.ConfigurationKeys(PackagingSmokeTests.EnvExample);
        var program = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.ProgramFile);

        Assert.Equal(new[] { "Db:Path", "Ops:LedgerPath" }, keys);

        foreach (var key in keys)
        {
            Assert.Contains("\"" + key + "\"", program, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_bearer_token_is_offered_but_not_armed()
    {
        var raw = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.EnvExample);
        var lines = PackagingSmokeTests.SettingLines(PackagingSmokeTests.EnvExample);

        Assert.Contains("DEALDESK_Auth__Token", raw, StringComparison.Ordinal);
        Assert.DoesNotContain(
            lines,
            line => line.StartsWith("DEALDESK_Auth__Token", StringComparison.Ordinal));
    }

    [Fact]
    public void The_listen_address_is_loopback_only()
    {
        var lines = PackagingSmokeTests.SettingLines(PackagingSmokeTests.EnvExample);
        var raw = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.EnvExample);

        var urlsLine = Assert.Single(
            lines,
            line => line.StartsWith("ASPNETCORE_URLS=", StringComparison.Ordinal));
        Assert.Contains("127.0.0.1", urlsLine, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0.0.0", raw, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_path_is_writable_by_the_unit()
    {
        var unit = PackagingSmokeTests.SettingLines(PackagingSmokeTests.UnitFile);
        var env = PackagingSmokeTests.SettingLines(PackagingSmokeTests.EnvExample);
        var rawEnv = PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.EnvExample);

        // The unit names exactly one writable directory.
        var rwPaths = unit
            .Where(line => line.StartsWith("ReadWritePaths=", StringComparison.Ordinal))
            .ToArray();
        Assert.Single(rwPaths);
        var writableDir = rwPaths[0].Split('=', 2)[1];

        Assert.Contains(writableDir, rawEnv, StringComparison.Ordinal);

        foreach (var line in env)
        {
            var eqIndex = line.IndexOf('=', StringComparison.Ordinal);
            if (eqIndex == -1) continue;

            var value = line[(eqIndex + 1)..];
            if (value.Length == 0 || value[0] != '/') continue;

            Assert.StartsWith(writableDir, value, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_bundle_extract_dir_is_set()
    {
        var lines = PackagingSmokeTests.SettingLines(PackagingSmokeTests.EnvExample);

        Assert.Single(
            lines,
            line => line.StartsWith("DOTNET_BUNDLE_EXTRACT_BASE_DIR=", StringComparison.Ordinal));
    }
}
