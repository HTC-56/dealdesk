using Dapper;
using DealDesk.Data;
using DealDesk.Domain;

namespace DealDesk.Api;

/// The offer surface: the two store numbers the desk types, and the priced
/// worksheet they produce.
///
/// `GET .../offer` stores nothing. It reads the comps, the recon lines and the
/// offer inputs as they stand, hands them to OfferMath, and serves the
/// recommended value together with every step that produced it. Nothing is
/// cached and no total is written back, so the number can never drift from the
/// rows it came from — that is what SPEC.md feature 2 means by "no magic
/// totals".
///
/// Pricing a worksheet with neither a comp nor a typed anchor is a 409, not a
/// 400: the request is fine, the worksheet simply is not ready to price yet.
public static class OfferEndpoints
{
    private const string OfferInputColumns =
        "appraisal_id, pack, target_gross, anchor_override, updated_at";

    public static void MapOfferEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapGet("/api/appraisals/{id:long}/offer-inputs", (Db db, long id) =>
        {
            using var connection = db.Open();

            if (!WorksheetEndpoints.AppraisalExists(connection, id))
            {
                return Results.NotFound();
            }

            // A worksheet the desk has never priced has no row yet. That is a
            // pack and a target gross of zero, not a 404 — the appraisal
            // exists, its store numbers are simply still untyped.
            return Results.Json(Stored(connection, id) ?? new OfferInputsView { AppraisalId = id });
        });

        routes.MapPut("/api/appraisals/{id:long}/offer-inputs", (Db db, long id, SaveOfferInputs body) =>
        {
            var error = body.Validate();
            if (error is not null)
            {
                return Results.BadRequest(new { error });
            }

            using var connection = db.Open();

            if (!WorksheetEndpoints.AppraisalExists(connection, id))
            {
                return Results.NotFound();
            }

            // At most one row per appraisal, so the write is an upsert: the
            // desk revises pack and target gross repeatedly on one worksheet.
            var row = connection.QuerySingle<OfferInputsView>(
                """
                INSERT INTO offer_input
                    (appraisal_id, pack, target_gross, anchor_override, updated_at)
                VALUES
                    ($id, $pack, $target, $anchor, $now)
                ON CONFLICT (appraisal_id) DO UPDATE SET
                    pack            = excluded.pack,
                    target_gross    = excluded.target_gross,
                    anchor_override = excluded.anchor_override,
                    updated_at      = excluded.updated_at
                RETURNING appraisal_id, pack, target_gross, anchor_override, updated_at;
                """,
                new
                {
                    id,
                    pack = body.Pack,
                    target = body.TargetGross,
                    anchor = body.AnchorOverride,
                    now = WorksheetEndpoints.Timestamp(),
                });

            return Results.Json(row);
        });

        routes.MapGet("/api/appraisals/{id:long}/offer", (Db db, long id) =>
        {
            using var connection = db.Open();

            if (!WorksheetEndpoints.AppraisalExists(connection, id))
            {
                return Results.NotFound();
            }

            var compPrices = connection.Query<long>(
                "SELECT price FROM comp WHERE appraisal_id = $id ORDER BY id;",
                new { id }).AsList();

            var reconEstimates = connection.Query<long>(
                "SELECT estimate FROM recon_line WHERE appraisal_id = $id ORDER BY id;",
                new { id }).AsList();

            var stored = Stored(connection, id);
            var pack = stored?.Pack ?? 0;
            var targetGross = stored?.TargetGross ?? 0;
            var anchorOverride = stored?.AnchorOverride;

            if (compPrices.Count == 0 && anchorOverride is null)
            {
                return Results.Conflict(new
                {
                    error = "this worksheet has no comps and no anchor override, "
                        + "so there is no market anchor to price from",
                });
            }

            var offer = OfferMath.Recommend(new OfferInputs
            {
                CompPrices = compPrices.ConvertAll(cents => new Money(cents)),
                AnchorOverride = anchorOverride is null ? null : new Money(anchorOverride.Value),
                ReconEstimates = reconEstimates.ConvertAll(cents => new Money(cents)),
                Pack = new Money(pack),
                TargetFrontGross = new Money(targetGross),
            });

            return Results.Json(new OfferView
            {
                AppraisalId = id,
                Anchor = offer.Anchor.Cents,
                AnchorOverridden = anchorOverride is not null,
                CompCount = anchorOverride is null ? compPrices.Count : 0,
                Recon = offer.Recon.Cents,
                Pack = pack,
                TargetGross = targetGross,
                Recommended = offer.Recommended.Cents,
                Derivation = offer.Derivation.Select(line => new DerivationLineView
                {
                    Label = line.Label,
                    Amount = line.Amount.Cents,
                    RunningTotal = line.RunningTotal.Cents,
                }).ToList(),
            });
        });
    }

    /// The stored offer inputs, or null when the desk has not typed any yet.
    private static OfferInputsView? Stored(System.Data.IDbConnection connection, long id) =>
        connection.QuerySingleOrDefault<OfferInputsView>(
            "SELECT " + OfferInputColumns + " FROM offer_input WHERE appraisal_id = $id;",
            new { id });
}
