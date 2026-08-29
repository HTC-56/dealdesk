namespace DealDesk.Api;

/// The worksheet's child collections as the API serves them, and the bodies it
/// accepts for them. Three rules hold across every record in this file:
///
/// * Money is a whole number of CENTS in a field with no suffix — `estimate`,
///   `price`, `pack`, `targetGross`, `anchorOverride`. The columns are INTEGER
///   cents (sql/002_worksheet.sql), the JSON is an integer of cents, and
///   nothing in between rounds. A client that wants dollars divides by 100.
/// * Every Create/Save body validates itself. Validate() returns null when the
///   body is acceptable and a one-line reason otherwise — exactly as
///   CreateAppraisal does, so an endpoint's whole job is turning that message
///   into a 400.
/// * The controlled vocabularies below are the same lists the CHECK
///   constraints enforce. Validating here turns a would-be 500 from SQLite
///   into a 400 that names the legal values.
internal static class Vocabulary
{
    internal static readonly string[] WalkAreas =
        ["exterior", "interior", "mechanical", "tires", "glass", "electronics", "other"];

    internal static readonly string[] WalkSeverities =
        ["minor", "moderate", "severe"];

    internal static readonly string[] ReconCategories =
        ["mechanical", "body", "paint", "tires", "glass", "interior", "detail", "other"];

    /// True when the trimmed value is in the list, ignoring case. Blank is
    /// never a member — callers that allow "omitted" check for that first.
    internal static bool Has(string[] allowed, string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Array.Exists(allowed, a => a.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));

    /// The form the column stores: trimmed and lowercase, or the fallback when
    /// the client omitted the field entirely.
    internal static string Canonical(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    /// The vocabulary as it appears inside an error message.
    internal static string List(string[] allowed) => string.Join(", ", allowed);
}

/// One line of the condition walk, as served.
public sealed record WalkItemView
{
    public long Id { get; init; }

    public long AppraisalId { get; init; }

    public string Area { get; init; } = string.Empty;

    public string Severity { get; init; } = string.Empty;

    public string Note { get; init; } = string.Empty;

    public string CreatedAt { get; init; } = string.Empty;
}

/// The body POST /api/appraisals/{id}/walk-items accepts. Severity is optional
/// and defaults to the mildest value, matching the column default.
public sealed record CreateWalkItem
{
    public string? Area { get; init; }

    public string? Severity { get; init; }

    public string? Note { get; init; }

    public string? Validate()
    {
        if (!Vocabulary.Has(Vocabulary.WalkAreas, Area))
        {
            return "area must be one of: " + Vocabulary.List(Vocabulary.WalkAreas);
        }

        if (!string.IsNullOrWhiteSpace(Severity)
            && !Vocabulary.Has(Vocabulary.WalkSeverities, Severity))
        {
            return "severity must be one of: " + Vocabulary.List(Vocabulary.WalkSeverities);
        }

        return null;
    }

    public string CanonicalArea() => Vocabulary.Canonical(Area, "other");

    public string CanonicalSeverity() => Vocabulary.Canonical(Severity, "minor");
}

/// One itemized line of the recon estimate, as served. `Estimate` is cents.
public sealed record ReconLineView
{
    public long Id { get; init; }

    public long AppraisalId { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public long Estimate { get; init; }

    public string CreatedAt { get; init; } = string.Empty;
}

/// The body POST /api/appraisals/{id}/recon-lines accepts.
///
/// A negative estimate is refused here as well as by the CHECK constraint: a
/// recon credit would quietly turn OfferMath's subtraction into an addition.
public sealed record CreateReconLine
{
    public string? Category { get; init; }

    public string? Description { get; init; }

    public long Estimate { get; init; }

    public string? Validate()
    {
        if (!Vocabulary.Has(Vocabulary.ReconCategories, Category))
        {
            return "category must be one of: " + Vocabulary.List(Vocabulary.ReconCategories);
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            return "description is required";
        }

        return Estimate < 0 ? "estimate cannot be negative (whole cents)" : null;
    }

    public string CanonicalCategory() => Vocabulary.Canonical(Category, "other");
}

/// One hand-entered comparable sale, as served. `Price` is cents.
public sealed record CompView
{
    public long Id { get; init; }

    public long AppraisalId { get; init; }

    public string Label { get; init; } = string.Empty;

    public int ModelYear { get; init; }

    public int Miles { get; init; }

    public long Price { get; init; }

    public string Note { get; init; } = string.Empty;

    public string CreatedAt { get; init; } = string.Empty;
}

/// The body POST /api/appraisals/{id}/comps accepts. Comps are typed by the
/// desk — dealdesk never fetches one (SPEC.md non-goals).
public sealed record CreateComp
{
    public string? Label { get; init; }

    public int ModelYear { get; init; }

    public int Miles { get; init; }

    public long Price { get; init; }

    public string? Note { get; init; }

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Label))
        {
            return "label is required";
        }

        if (ModelYear is < 1900 or > 2200)
        {
            return "modelYear must be between 1900 and 2200";
        }

        if (Miles < 0)
        {
            return "miles cannot be negative";
        }

        return Price <= 0 ? "price must be positive (whole cents)" : null;
    }
}

/// The two store numbers plus the optional hand-typed anchor, as served. All
/// three are cents; a worksheet the desk has never touched reads as zeros.
public sealed record OfferInputsView
{
    public long AppraisalId { get; init; }

    public long Pack { get; init; }

    public long TargetGross { get; init; }

    public long? AnchorOverride { get; init; }

    public string UpdatedAt { get; init; } = string.Empty;
}

/// The body PUT /api/appraisals/{id}/offer-inputs accepts. There is at most one
/// such row per appraisal, so the write is an upsert rather than a POST.
public sealed record SaveOfferInputs
{
    public long Pack { get; init; }

    public long TargetGross { get; init; }

    public long? AnchorOverride { get; init; }

    public string? Validate()
    {
        if (Pack < 0)
        {
            return "pack cannot be negative (whole cents)";
        }

        if (TargetGross < 0)
        {
            return "targetGross cannot be negative (whole cents)";
        }

        return AnchorOverride is <= 0
            ? "anchorOverride must be positive (whole cents) when present"
            : null;
    }
}

/// One labelled step of the derivation. `Amount` is signed — the subtractions
/// arrive negative — and `RunningTotal` is where the worksheet stands after it.
public sealed record DerivationLineView
{
    public string Label { get; init; } = string.Empty;

    public long Amount { get; init; }

    public long RunningTotal { get; init; }
}

/// A priced worksheet: the recommended trade value and every step that produced
/// it. The two always travel together — that pairing is what SPEC.md feature 2
/// means by "no magic totals", so nothing here is served without Derivation.
public sealed record OfferView
{
    public long AppraisalId { get; init; }

    /// What the market said, in cents — the comp average unless the desk typed
    /// an anchor of its own.
    public long Anchor { get; init; }

    public bool AnchorOverridden { get; init; }

    /// How many comps the average came from; 0 when the anchor was typed.
    public int CompCount { get; init; }

    public long Recon { get; init; }

    public long Pack { get; init; }

    public long TargetGross { get; init; }

    public long Recommended { get; init; }

    public IReadOnlyList<DerivationLineView> Derivation { get; init; } = [];
}
