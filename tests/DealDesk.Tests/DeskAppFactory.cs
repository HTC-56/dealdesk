using DealDesk.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DealDesk.Tests;

/// Boots the real Program in-process against a throwaway SQLite file, so the
/// integration tests exercise the same wiring the published binary runs.
/// Nothing here listens on a port and nothing leaves the machine.
internal sealed class DeskAppFactory : WebApplicationFactory<Program>
{
    private readonly string directory =
        Path.Combine(Path.GetTempPath(), "dealdesk-app-" + Guid.NewGuid().ToString("N"));

    private readonly string? authToken;

    /// Writes are open, exactly as a fresh clone runs them.
    public DeskAppFactory()
        : this(null)
    {
    }

    /// Arms the static bearer token on write endpoints. Only the ops tests
    /// pass one; every other test file boots the open app.
    public DeskAppFactory(string? authToken) => this.authToken = authToken;

    /// The JSONL ops ledger this instance writes to. Inside the throwaway
    /// directory, so a test run leaves no ledger behind and two factories never
    /// read each other's lines.
    public string LedgerPath => Path.Combine(directory, "ledger.jsonl");

    /// Writes the demo month into this instance's database — the same script
    /// `dotnet run --project src/DealDesk -- seed` runs. Touching Services boots
    /// the host, so the schema is already current when the seed lands. Returns
    /// how many worksheets it wrote, or 0 if this factory was seeded already.
    public int Seed() => Seeder.Apply(Services.GetRequiredService<Db>());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Directory.CreateDirectory(directory);
        builder.UseSetting("Db:Path", Path.Combine(directory, "app.db"));
        builder.UseSetting("Ops:LedgerPath", LedgerPath);

        if (!string.IsNullOrWhiteSpace(authToken))
        {
            builder.UseSetting("Auth:Token", authToken);
        }
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
