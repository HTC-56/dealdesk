using Dapper;
using DealDesk.Api;
using DealDesk.Data;
using DealDesk.Ops;

var builder = WebApplication.CreateBuilder(args);

// DEALDESK_Db__Path overrides appsettings; tests point it at a temp file.
builder.Configuration.AddEnvironmentVariables("DEALDESK_");

var dbPath = builder.Configuration["Db:Path"] ?? "dealdesk.db";
builder.Services.AddSingleton(new Db(dbPath));

// The ops surface: in-memory counters for /metrics and a JSONL line per
// request. DEALDESK_Ops__LedgerPath moves the file; blanking it turns the
// ledger off for a deployment that ships its own request log.
builder.Services.AddSingleton<OpsMetrics>();
builder.Services.AddSingleton(
    new OpsLedger(builder.Configuration["Ops:LedgerPath"] ?? "ledger.jsonl"));

var app = builder.Build();

// Schema is brought current at startup: a fresh clone runs with no setup step.
app.Services.GetRequiredService<Db>().Migrate();

// Recording wraps the token check so a refused write is still counted and
// written down. DEALDESK_Auth__Token arms the guard; unset leaves writes open,
// which is what the README quickstart runs against.
app.UseOpsRecording();
app.UseBearerTokenOnWrites(app.Configuration["Auth:Token"]);

app.MapGet("/healthz", (Db db) =>
{
    using var connection = db.Open();
    var schema = connection.QuerySingleOrDefault<string>(
        "SELECT id FROM schema_migrations ORDER BY id DESC LIMIT 1;");

    return Results.Json(new
    {
        status = "ok",
        schema = schema ?? "none",
    });
});

// Worksheet endpoints live under Api/ so this file stays a wiring file rather
// than growing a handler per feature: the appraisal itself, its child
// collections, the offer math that prices them, and the reports that read
// across every worksheet at once.
app.MapAppraisalEndpoints();
app.MapWorksheetEndpoints();
app.MapOfferEndpoints();
app.MapLifecycleEndpoints();
app.MapReconEndpoints();
app.MapReportEndpoints();
app.MapOpsEndpoints();

app.Run();

/// Named so the in-process integration tests can boot this exact app through
/// WebApplicationFactory rather than a second, drifting copy of the wiring.
public partial class Program
{
    protected Program()
    {
    }
}
