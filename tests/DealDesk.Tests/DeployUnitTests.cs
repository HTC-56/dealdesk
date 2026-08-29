using Xunit;

namespace DealDesk.Tests;

/// SPEC.md feature 9 — deploy-grade packaging — systemd unit file.
/// These six facts check that the example unit would actually start
/// dealdesk on a real host, rather than being documentation that lies.
public sealed class DeployUnitTests
{
    /// The example systemd unit, read through PackagingSmokeTests' helper.
    private const string UnitFile = PackagingSmokeTests.UnitFile;

    /// One repo file as its settings: non-blank lines with the comments and the
    /// indentation taken off.
    private static string[] Lines =>
        PackagingSmokeTests.SettingLines(UnitFile);

    /// The three sections appear in order: [Unit], [Service], [Install].
    [Fact]
    public void The_sections_appear_in_order()
    {
        var lines = Lines;

        var unitIndex = Array.FindIndex(lines, l => l == "[Unit]");
        var serviceIndex = Array.FindIndex(lines, l => l == "[Service]");
        var installIndex = Array.FindIndex(lines, l => l == "[Install]");

        Assert.True(unitIndex >= 0, "[Unit] section header not found");
        Assert.True(serviceIndex >= 0, "[Service] section header not found");
        Assert.True(installIndex >= 0, "[Install] section header not found");

        Assert.True(unitIndex < serviceIndex, "[Unit] must come before [Service]");
        Assert.True(serviceIndex < installIndex, "[Service] must come before [Install]");
    }

    /// The type is exec, not notify. dealdesk is a plain Kestrel host and
    /// never signals readiness to systemd; a notify unit would sit there
    /// until it timed out.
    [Fact]
    public void The_type_is_exec_not_notify()
    {
        var lines = Lines;

        Assert.Contains("Type=exec", lines);
        Assert.DoesNotContain("Type=notify", lines);
    }

    /// It runs unprivileged: a dedicated user and group, no-new-privileges,
    /// and no line names root.
    [Fact]
    public void It_runs_unprivileged()
    {
        var lines = Lines;

        Assert.Contains("User=dealdesk", lines);
        Assert.Contains("Group=dealdesk", lines);
        Assert.Contains("NoNewPrivileges=true", lines);
        Assert.DoesNotContain("User=root", lines);
    }

    /// The filesystem is closed except the one place it writes.
    [Fact]
    public void The_filesystem_is_closed_except_the_writable_dir()
    {
        var lines = Lines;

        Assert.Contains("ProtectSystem=strict", lines);
        Assert.Contains("ProtectHome=true", lines);
        Assert.Contains("PrivateTmp=true", lines);

        var rwPaths = lines.Where(l => l.StartsWith("ReadWritePaths=", StringComparison.Ordinal)).ToArray();
        Assert.Single(rwPaths);
        Assert.Contains("/var/lib/dealdesk", rwPaths[0]);
    }

    /// A crash is recovered, not mourned.
    [Fact]
    public void A_crash_is_recovered()
    {
        var lines = Lines;

        Assert.Contains("Restart=on-failure", lines);

        var restartSec = Assert.Single(lines.Where(l => l.StartsWith("RestartSec=", StringComparison.Ordinal)));
        Assert.NotEmpty(restartSec);
    }

    /// Every path it names is absolute. systemd refuses a relative path, so
    /// an example carrying one would fail on the host and pass every gate
    /// here.
    [Fact]
    public void Every_path_is_absolute()
    {
        var lines = Lines;

        var pathKeys = new[] { "ExecStart=", "WorkingDirectory=", "EnvironmentFile=", "ReadWritePaths=" };

        foreach (var prefix in pathKeys)
        {
            var matching = lines.Where(l => l.StartsWith(prefix, StringComparison.Ordinal)).ToArray();

            foreach (var line in matching)
            {
                var value = line[prefix.Length..];
                Assert.StartsWith("/", value, StringComparison.Ordinal);
            }
        }
    }
}
