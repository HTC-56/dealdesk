using Dapper;
using DealDesk.Data;

namespace DealDesk.Api;

/// The worksheet endpoints. Each one opens its own connection and runs plain
/// parameterised SQL — the SQL is part of the work sample, so nothing here
/// hides it behind a repository layer.
///
/// Write endpoints are open for now; the static bearer token that guards them
/// is SPEC.md feature 7 and arrives with the rest of the ops surface.
public static class AppraisalEndpoints
{
    /// Every appraisal query selects exactly these, in schema order. A const
    /// so the SQL stays a compile-time literal.
    private const string Columns =
        "id, vin, model_year, make, model, trim_level, miles, appraiser, " +
        "status, created_at, updated_at";

    public static void MapAppraisalEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet("/api/appraisals/{id:long}", (Db db, long id) =>
        {
            using var connection = db.Open();

            var row = connection.QuerySingleOrDefault<AppraisalView>(
                "SELECT " + Columns + " FROM appraisal WHERE id = $id;",
                new { id });

            return row is null ? Results.NotFound() : Results.Json(row);
        });

        routes.MapGet("/api/appraisals", (Db db, string? status) =>
        {
            using var connection = db.Open();

            var sql = string.IsNullOrWhiteSpace(status)
                ? "SELECT " + Columns + " FROM appraisal ORDER BY id DESC"
                : "SELECT " + Columns + " FROM appraisal WHERE status = $status ORDER BY id DESC";

            var rows = connection.Query<AppraisalView>(
                sql,
                new { status });

            return Results.Json(rows);
        });
    }
}
