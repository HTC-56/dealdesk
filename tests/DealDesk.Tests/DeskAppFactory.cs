using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DealDesk.Tests;

/// Boots the real Program in-process against a throwaway SQLite file, so the
/// integration tests exercise the same wiring the published binary runs.
/// Nothing here listens on a port and nothing leaves the machine.
internal sealed class DeskAppFactory : WebApplicationFactory<Program>
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "dealdesk-app-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Directory.CreateDirectory(directory);
        builder.UseSetting("Db:Path", Path.Combine(directory, "app.db"));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // A leftover temp file is not a test failure.
        }
    }
}
