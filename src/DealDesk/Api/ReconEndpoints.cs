using Dapper;
using DealDesk.Data;
using DealDesk.Domain;
using Microsoft.Data.Sqlite;

namespace DealDesk.Api;

/// Recon actuals and the variance they produce — SPEC.md feature 4.
///
/// Postings hang off a recon LINE, not off the appraisal, so every route here
/// is `/api/appraisals/{id}/recon-lines/{lineId}/...` and every one of them
/// checks that the line really belongs to that appraisal. A line id from
/// someone else's worksheet is a 404, never a posting written against the
/// wrong car.
///
/// `GET .../recon-variance` stores nothing and caches nothing, exactly like
/// `GET .../offer`: it reads the estimate lines and the postings as they
/// stand, hands both to ReconVariance, and serves the result. No variance is
/// ever written to a column, so it cannot drift from the rows it came from.
/// SPEC.md calls variance "first-class data, not a report afterthought" —
/// first-class here means it is derived from the same rows every time anyone
/// asks, and that the arithmetic lives in Domain/ where the tests can reach it
/// without HTTP.
///
/// Write endpoints are open for now; the static bearer token that guards them
/// is SPEC.md feature 7 and arrives with the rest of the ops surface.
public static class ReconEndpoints
{
    /// Selected in schema order so Dapper's underscore matching does the
    /// mapping and no query carries an AS alias.
    internal const string ReconActualColumns =
        "id, recon_line_id, amount, description, posted_by, posted_at";

    public static void MapReconEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet("/api/appraisals/{id:long}/recon-variance", (Db db, long id) =>
        {
            using var connection = db.Open();

            if (!WorksheetEndpoints.AppraisalExists(connection, id))
            {
                return Results.NotFound();
            }

            var lines = connection.Query<ReconLineView>(
                "SELECT id, appraisal_id, category, description, estimate, created_at" +
                " FROM recon_line WHERE appraisal_id = $id ORDER BY id;",
                new { id }).AsList();

            // Read in one hop through the line table rather than joining and
            // aggregating in SQL: the grouping is the domain's job, and this
            // way the endpoint hands ReconVariance exactly the rows the tests
            // hand it too.
            var postings = connection.Query<ReconActualView>(
                "SELECT " + ReconActualColumns + " FROM recon_actual" +
                " WHERE recon_line_id IN (SELECT id FROM recon_line WHERE appraisal_id = $id)" +
                " ORDER BY id;",
                new { id }).AsList();

            // A worksheet with no recon lines is not a refusal: it estimated
            // nothing, spent nothing, and is zero over. That differs from the
            // offer endpoint's 409 because an anchor cannot be invented from
            // no comps, while an empty sum is genuinely zero.
            var summary = ReconVariance.Summarise(
                lines.ConvertAll(line => new ReconEstimateLine
                {
                    LineId = line.Id,
                    Category = line.Category,
                    Description = line.Description,
                    Estimate = new Money(line.Estimate),
                }),
                postings.ConvertAll(posting =>
                    new ReconPosting(posting.ReconLineId, new Money(posting.Amount))));

            return Results.Json(new ReconVarianceView
            {
                AppraisalId = id,
                TotalEstimate = summary.TotalEstimate.Cents,
                TotalActual = summary.TotalActual.Cents,
                TotalVariance = summary.TotalVariance.Cents,
                UnpostedLines = summary.UnpostedLines,
                Lines = summary.Lines.Select(line => new ReconLineVarianceView
                {
                    LineId = line.LineId,
                    Category = line.Category,
                    Description = line.Description,
                    Estimate = line.Estimate.Cents,
                    Actual = line.Actual.Cents,
                    Variance = line.Variance.Cents,
                    PostingCount = line.PostingCount,
                    Posted = line.Posted,
                }).ToList(),
                ByCategory = summary.ByCategory.Select(group => new ReconCategoryVarianceView
                {
                    Category = group.Category,
                    Estimate = group.Estimate.Cents,
                    Actual = group.Actual.Cents,
                    Variance = group.Variance.Cents,
                    LineCount = group.LineCount,
                }).ToList(),
            });
        });
    }

    /// Whether that recon line exists AND hangs off that appraisal. The
    /// posting routes call this before reading or writing, so a line id
    /// belonging to another worksheet is a 404 rather than a posting filed
    /// against the wrong vehicle.
    internal static bool ReconLineBelongs(SqliteConnection connection, long appraisalId, long lineId) =>
        connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM recon_line WHERE id = $lineId AND appraisal_id = $appraisalId;",
            new { lineId, appraisalId }) > 0;
}
