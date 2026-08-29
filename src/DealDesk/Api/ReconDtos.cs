namespace DealDesk.Api;

/// Recon actuals and the variance they produce, as the API serves them. The
/// three rules from WorksheetDtos.cs still hold: money is whole CENTS in a
/// field with no suffix, every posted body validates itself into a one-line
/// reason, and the vocabularies match the CHECK constraints.
///
/// One rule is new, and it is the whole point of this file: `variance` is
/// ALWAYS `actual - estimate`. Positive means the line ran over what was
/// estimated. Every variance field below — per line, per category, and the
/// worksheet total — carries that same sign, so a reader learns the convention
/// once.

/// One posted actual cost, as served. `Amount` is cents and may be negative.
public sealed record ReconActualView
{
    public long Id { get; init; }

    public long ReconLineId { get; init; }

    public long Amount { get; init; }

    public string Description { get; init; } = string.Empty;

    public string PostedBy { get; init; } = string.Empty;

    public string PostedAt { get; init; } = string.Empty;
}

/// The body POST /api/appraisals/{id}/recon-lines/{lineId}/actuals accepts.
///
/// A negative amount is deliberately legal — that is a supplier credit or a
/// returned part, and OfferMath.cs says a recon credit belongs here rather
/// than in an estimate line. Zero is refused: a posting that moves no money
/// tells the variance nothing and would still count as a posting.
public sealed record PostReconActual
{
    public long Amount { get; init; }

    public string? Description { get; init; }

    public string? PostedBy { get; init; }

    public string? Validate()
    {
        if (Amount == 0)
        {
            return "amount must not be zero (whole cents; a credit is negative)";
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            return "description is required";
        }

        return string.IsNullOrWhiteSpace(PostedBy) ? "postedBy is required" : null;
    }
}

/// One estimate line measured against what was actually spent on it.
public sealed record ReconLineVarianceView
{
    public long LineId { get; init; }

    public string Category { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public long Estimate { get; init; }

    public long Actual { get; init; }

    /// `actual - estimate`; positive means over estimate.
    public long Variance { get; init; }

    public int PostingCount { get; init; }

    /// False when nothing has been spent against the line yet — which is not
    /// the same thing as coming in under budget.
    public bool Posted { get; init; }
}

/// The same three numbers rolled up to one recon category, for the by-category
/// reading SPEC.md feature 5 reports on.
public sealed record ReconCategoryVarianceView
{
    public string Category { get; init; } = string.Empty;

    public long Estimate { get; init; }

    public long Actual { get; init; }

    public long Variance { get; init; }

    public int LineCount { get; init; }
}

/// A whole worksheet's recon variance. Like the offer, the totals never travel
/// without the lines that produced them.
public sealed record ReconVarianceView
{
    public long AppraisalId { get; init; }

    public long TotalEstimate { get; init; }

    public long TotalActual { get; init; }

    public long TotalVariance { get; init; }

    /// How many lines nothing has posted against yet. While this is above
    /// zero, recon is unfinished and the total is not the final number.
    public int UnpostedLines { get; init; }

    public IReadOnlyList<ReconLineVarianceView> Lines { get; init; } = [];

    public IReadOnlyList<ReconCategoryVarianceView> ByCategory { get; init; } = [];
}
