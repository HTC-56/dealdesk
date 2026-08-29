using Dapper;
using DealDesk.Api;
using DealDesk.Data;

var builder = WebApplication.CreateBuilder(args);

// DEALDESK_Db__Path overrides appsettings; tests point it at a temp file.
builder.Configuration.AddEnvironmentVariables("DEALDESK_");

var dbPath = builder.Configuration["Db:Path"] ?? "dealdesk.db";
builder.Services.AddSingleton(new Db(dbPath));

var app = builder.Build();

// Schema is brought current at startup: a fresh clone runs with no setup step.
app.Services.GetRequiredService<Db>().Migrate();

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
// collections, and the offer math that prices them.
app.MapAppraisalEndpoints();
app.MapWorksheetEndpoints();

app.Run();

/// Named so the in-process integration tests can boot this exact app through
/// WebApplicationFactory rather than a second, drifting copy of the wiring.
public partial class Program
{
    protected Program()
    {
    }
}
