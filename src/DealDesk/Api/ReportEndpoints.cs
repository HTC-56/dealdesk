using Dapper;
using DealDesk.Data;

namespace DealDesk.Api;

/// The three reports — SPEC.md feature 5. Every route here is
/// `GET /api/reports/...`, takes no id, and reads one SQL view from
/// sql/005_reports.sql.
///
/// The views do the grouping and the endpoint adds only the store-wide totals,
/// which is the division SPEC.md asks for when it says "SQL views + endpoints".
/// A handler that re-derived a report in C# would be a second definition of
/// the same number; there is exactly one, and it is in the migration where a
/// reader can see the arithmetic.
///
/// Nothing here is per-appraisal, so nothing here 404s: an empty store is an
/// empty report, and a report with no rows still carries its zero totals. That
/// matches `GET .../recon-variance` on a worksheet with no recon lines and for
/// the same reason — an empty sum is genuinely zero.
public static class ReportEndpoints
{
    /// Selected in view order so Dapper's underscore matching does the mapping
    /// and no query carries an AS alias.
    internal const string LookToBookColumns =
        "appraiser, looked, appraised, booked, lost, open_worksheets";

    internal const string ReconVarianceColumns =
        "category, line_count, estimate, actual, variance, unposted_lines";

    internal const string FrontGrossColumns =
        "appraiser, won_count, target_gross, recon_variance, projected_gross, unposted_lines";

    public static void MapReportEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet("/api/reports/look-to-book", (Db db) =>
        {
            using var connection = db.Open();

            var rows = connection.Query<LookToBookRow>(
                "SELECT " + LookToBookColumns +
                " FROM report_look_to_book ORDER BY appraiser;").AsList();

            // Totals are summed from the same rows the caller receives rather
            // than queried separately, so the header of the report and its
            // body cannot come from two different reads of the table.
            return Results.Json(new LookToBookReport
            {
                Looked = rows.Sum(row => row.Looked),
                Appraised = rows.Sum(row => row.Appraised),
                Booked = rows.Sum(row => row.Booked),
                Lost = rows.Sum(row => row.Lost),
                OpenWorksheets = rows.Sum(row => row.OpenWorksheets),
                Rows = rows,
            });
        });

        routes.MapGet("/api/reports/recon-variance", (Db db) =>
        {
            using var connection = db.Open();

            var rows = connection.Query<ReconVarianceReportRow>(
                "SELECT " + ReconVarianceColumns +
                " FROM report_recon_variance ORDER BY category;").AsList();

            return Results.Json(new ReconVarianceReport
            {
                TotalEstimate = rows.Sum(row => row.Estimate),
                TotalActual = rows.Sum(row => row.Actual),
                TotalVariance = rows.Sum(row => row.Variance),
                UnpostedLines = rows.Sum(row => row.UnpostedLines),
                Rows = rows,
            });
        });
    }
}
