using System.Globalization;
using Dapper;
using DealDesk.Data;
using DealDesk.Domain;
using Microsoft.Data.Sqlite;

namespace DealDesk.Api;

/// The worksheet lifecycle and the trail it leaves — SPEC.md feature 3.
///
/// Two routes write, and neither can write without leaving a record: the status
/// move and the field revision each update the appraisal row and append to
/// `audit_entry` inside ONE transaction. If the audit insert fails, the change
/// fails with it, so there is no path through this file that alters a value
/// without saying who changed it and why.
///
/// The trail records one row per FIELD. A revision that moves miles and the
/// appraiser writes two rows; a revision that supplies a value already equal to
/// the stored one writes none, because nothing moved.
///
/// A status word this file does not know is a 400 (the request is malformed);
/// a real status the lifecycle will not move to from here is a 409 (the request
/// is fine, the worksheet just is not there). That split matches the 409 the
/// offer endpoint returns for a worksheet with no anchor.
///
/// Write endpoints are open for now; the static bearer token that guards them
/// is SPEC.md feature 7 and arrives with the rest of the ops surface.
public static class LifecycleEndpoints
{
    public static void MapLifecycleEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapPost("/api/appraisals/{id:long}/status", (Db db, long id, ChangeStatus body) =>
        {
            var error = body.Validate();
            if (error is not null)
            {
                return Results.BadRequest(new { error });
            }

            using var connection = db.Open();

            var current = connection.QuerySingleOrDefault<string>(
                "SELECT status FROM appraisal WHERE id = $id;",
                new { id });

            if (current is null)
            {
                return Results.NotFound();
            }

            var wanted = body.CanonicalStatus();

            var refusal = Lifecycle.Refuse(current, wanted);
            if (refusal is not null)
            {
                return Results.Conflict(new { error = refusal });
            }

            var now = WorksheetEndpoints.Timestamp();

            using var tx = connection.BeginTransaction();

            connection.Execute(
                "UPDATE appraisal SET status = $wanted, updated_at = $now WHERE id = $id;",
                new { wanted, now, id },
                tx);

            Record(connection, tx, id, "status", current, wanted, body.ChangedBy!, body.Reason!, now);

            var row = connection.QuerySingle<AppraisalView>(
                "SELECT " + AppraisalEndpoints.Columns + " FROM appraisal WHERE id = $id;",
                new { id },
                tx);

            tx.Commit();

            return Results.Json(row);
        });

        routes.MapPatch("/api/appraisals/{id:long}", (Db db, long id, ReviseAppraisal body) =>
        {
            var error = body.Validate();
            if (error is not null)
            {
                return Results.BadRequest(new { error });
            }

            using var connection = db.Open();

            var current = connection.QuerySingleOrDefault<AppraisalView>(
                "SELECT " + AppraisalEndpoints.Columns + " FROM appraisal WHERE id = $id;",
                new { id });

            if (current is null)
            {
                return Results.NotFound();
            }

            // Supplying a value that already matches is not a change. Nothing is
            // written, and the 200 hands back the row exactly as it stands.
            var changes = Changes(current, body);
            if (changes.Count == 0)
            {
                return Results.Json(current);
            }

            var now = WorksheetEndpoints.Timestamp();

            using var tx = connection.BeginTransaction();

            // One fixed statement rather than an assembled SET clause: every
            // untouched column is rewritten with the value it already holds, so
            // the SQL here stays a compile-time literal like the rest of the repo.
            var row = connection.QuerySingle<AppraisalView>(
                """
                UPDATE appraisal SET
                    model_year = $modelYear,
                    make       = $make,
                    model      = $model,
                    trim_level = $trimLevel,
                    miles      = $miles,
                    appraiser  = $appraiser,
                    updated_at = $now
                WHERE id = $id
                RETURNING id, vin, model_year, make, model, trim_level, miles,
                          appraiser, status, created_at, updated_at;
                """,
                new
                {
                    modelYear = body.ModelYear ?? current.ModelYear,
                    make = body.Make?.Trim() ?? current.Make,
                    model = body.Model?.Trim() ?? current.Model,
                    trimLevel = body.TrimLevel?.Trim() ?? current.TrimLevel,
                    miles = body.Miles ?? current.Miles,
                    appraiser = body.Appraiser?.Trim() ?? current.Appraiser,
                    now,
                    id,
                },
                tx);

            foreach (var change in changes)
            {
                Record(
                    connection, tx, id, change.Field, change.OldValue, change.NewValue,
                    body.ChangedBy!, body.Reason!, now);
            }

            tx.Commit();

            return Results.Json(row);
        });

        routes.MapGet("/api/appraisals/{id:long}/audit", (Db db, long id) =>
        {
            using var connection = db.Open();

            if (!WorksheetEndpoints.AppraisalExists(connection, id))
            {
                return Results.NotFound();
            }

            var rows = connection.Query<AuditEntryView>(
                "SELECT " + AuditColumns +
                " FROM audit_entry WHERE appraisal_id = $id ORDER BY id DESC;",
                new { id });

            return Results.Json(rows);
        });
    }

    /// Selected in schema order so Dapper's underscore matching does the
    /// mapping and the query carries no AS alias.
    internal const string AuditColumns =
        "id, appraisal_id, field, old_value, new_value, changed_by, reason, changed_at";

    /// Appends one row to the trail. Always called inside the same transaction
    /// as the change it describes — that pairing is the whole feature, so this
    /// takes the transaction rather than opening anything of its own.
    private static void Record(
        SqliteConnection connection,
        SqliteTransaction tx,
        long appraisalId,
        string field,
        string oldValue,
        string newValue,
        string changedBy,
        string reason,
        string at) =>
        connection.Execute(
            """
            INSERT INTO audit_entry
                (appraisal_id, field, old_value, new_value, changed_by, reason, changed_at)
            VALUES
                ($appraisalId, $field, $oldValue, $newValue, $changedBy, $reason, $at);
            """,
            new
            {
                appraisalId,
                field,
                oldValue,
                newValue,
                changedBy = changedBy.Trim(),
                reason = reason.Trim(),
                at,
            },
            tx);

    /// Which fields the body actually moves, as the trail will name them: the
    /// JSON field name, the value now, and the value asked for. A field the body
    /// left null is untouched and never appears here.
    private static List<FieldChange> Changes(AppraisalView current, ReviseAppraisal body)
    {
        var changes = new List<FieldChange>();

        Consider(changes, "modelYear", Text(current.ModelYear), Text(body.ModelYear));
        Consider(changes, "make", current.Make, body.Make?.Trim());
        Consider(changes, "model", current.Model, body.Model?.Trim());
        Consider(changes, "trimLevel", current.TrimLevel, body.TrimLevel?.Trim());
        Consider(changes, "miles", Text(current.Miles), Text(body.Miles));
        Consider(changes, "appraiser", current.Appraiser, body.Appraiser?.Trim());

        return changes;
    }

    private static void Consider(List<FieldChange> into, string field, string old, string? wanted)
    {
        if (wanted is not null && !string.Equals(old, wanted, StringComparison.Ordinal))
        {
            into.Add(new FieldChange(field, old, wanted));
        }
    }

    /// The trail stores every value as text, so the two integer fields render
    /// the same way going in and coming out.
    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string? Text(int? value) =>
        value is null ? null : Text(value.Value);

    private sealed record FieldChange(string Field, string OldValue, string NewValue);
}
