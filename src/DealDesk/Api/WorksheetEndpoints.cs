using Dapper;
using DealDesk.Data;
using Microsoft.Data.Sqlite;

namespace DealDesk.Api;

/// The worksheet's child collections: the condition walk, the itemized recon
/// estimate, and the hand-entered comps. Every route hangs off one appraisal —
/// `/api/appraisals/{id}/...` — because a walk line with no vehicle is
/// meaningless, and the rows cascade-delete with their parent.
///
/// Each handler opens its own connection and runs plain parameterised SQL, the
/// same shape AppraisalEndpoints uses; the SQL is part of the work sample, so
/// nothing here hides behind a repository layer.
///
/// Two behaviours are the same on every route below, and the tests pin both:
/// an unknown appraisal id is a 404 (not an empty list), and a body its own
/// Validate() rejects is a 400 carrying `error` — so a bad category never
/// reaches SQLite as a CHECK violation.
///
/// Write endpoints are open for now; the static bearer token that guards them
/// is SPEC.md feature 7 and arrives with the rest of the ops surface.
public static class WorksheetEndpoints
{
    /// Selected in schema order so Dapper's underscore matching does the
    /// mapping and no query carries an AS alias. A const keeps the SQL a
    /// compile-time literal.
    private const string WalkItemColumns =
        "id, appraisal_id, area, severity, note, created_at";

    public static void MapWorksheetEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet("/api/appraisals/{id:long}/walk-items", (Db db, long id) =>
        {
            using var connection = db.Open();

            if (!AppraisalExists(connection, id))
            {
                return Results.NotFound();
            }

            var rows = connection.Query<WalkItemView>(
                "SELECT " + WalkItemColumns +
                " FROM walk_item WHERE appraisal_id = $id ORDER BY id;",
                new { id });

            return Results.Json(rows);
        });

        routes.MapPost("/api/appraisals/{id:long}/walk-items", (Db db, long id, CreateWalkItem body) =>
        {
            var error = body.Validate();
            if (error is not null)
            {
                return Results.BadRequest(new { error });
            }

            using var connection = db.Open();

            if (!AppraisalExists(connection, id))
            {
                return Results.NotFound();
            }

            // RETURNING hands back the stored row, so the 201 body is what a
            // subsequent GET would show — defaults applied, nothing guessed.
            var row = connection.QuerySingle<WalkItemView>(
                """
                INSERT INTO walk_item (appraisal_id, area, severity, note, created_at)
                VALUES ($id, $area, $severity, $note, $now)
                RETURNING id, appraisal_id, area, severity, note, created_at;
                """,
                new
                {
                    id,
                    area = body.CanonicalArea(),
                    severity = body.CanonicalSeverity(),
                    note = body.Note ?? string.Empty,
                    now = Timestamp(),
                });

            return Results.Created($"/api/appraisals/{id}/walk-items/{row.Id}", row);
        });
    }

    /// Whether the parent worksheet is really there. Child routes call this
    /// before reading or writing so a stray id is a 404 rather than an empty
    /// list or a foreign-key 500.
    internal static bool AppraisalExists(SqliteConnection connection, long id) =>
        connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM appraisal WHERE id = $id;",
            new { id }) > 0;

    /// The one timestamp format the whole schema stores: round-trippable ISO
    /// 8601 in UTC, so string ordering is time ordering.
    internal static string Timestamp() => DateTimeOffset.UtcNow.ToString("O");
}
