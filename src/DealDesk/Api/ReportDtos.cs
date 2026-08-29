namespace DealDesk.Api;

/// The three reports of SPEC.md feature 5, as the API serves them. Each one is
/// a `{ …store totals…, rows: [...] }` object rather than a bare array: a
/// director opens a report to read one number and then find out who it came
/// from, and a total that travels with its rows cannot disagree with them.
///
/// Two rules carry over from the rest of the API. Money is a whole number of
/// CENTS in a field with no suffix — `estimate`, `actual`, `variance`,
/// `targetGross`, `projectedGross`. And `variance` is always
/// `actual - estimate`, positive meaning over estimate, exactly as
/// Domain/ReconVariance.cs fixed it for one worksheet.
///
/// One rule is new. A RATE is served in basis points — hundredths of a
/// percent, as an integer — never as a fraction. `bookRateBps` of `2500` is
/// 25.00%. A JSON number is a double in a browser, and the repo already
/// refuses to put `0.25` on the wire for the same reason it refuses `1234.56`.

/// Turns counted things into the integer rates the reports serve.
internal static class Reports
{
    /// `part` of `whole`, in basis points: hundredths of one percent, so
    /// 10_000 is all of it. Truncated toward zero — a rate is a reading, not
    /// money, so no cent is lost by rounding it down. A whole of zero is 0
    /// rather than an error: an appraiser with nothing appraised has no rate
    /// yet, which is a fact about the month, not a division to refuse.
    internal static int RateBps(int part, int whole) =>
        whole == 0 ? 0 : (int)(part * 10_000L / whole);
}

/// One appraiser's look-to-book. `Looked` counts every worksheet they opened;
/// `Appraised` counts the ones that actually reached a price (read off the
/// audit trail when the worksheet was lost early); `Booked` is what the store
/// bought.
public sealed record LookToBookRow
{
    public string Appraiser { get; init; } = string.Empty;

    public int Looked { get; init; }

    public int Appraised { get; init; }

    public int Booked { get; init; }

    public int Lost { get; init; }

    /// Worksheets still live — draft, appraised or presented. A month with
    /// many of these has not settled, and its rate will still move.
    public int OpenWorksheets { get; init; }

    /// Booked as a share of appraised, in basis points. Appraised is the
    /// denominator SPEC.md names ("appraised vs won"); a worksheet abandoned
    /// in draft was never a real look at the car.
    public int BookRateBps => Reports.RateBps(Booked, Appraised);
}

/// The whole store's look-to-book, with the appraisers who make it up.
public sealed record LookToBookReport
{
    public int Looked { get; init; }

    public int Appraised { get; init; }

    public int Booked { get; init; }

    public int Lost { get; init; }

    public int OpenWorksheets { get; init; }

    public int BookRateBps => Reports.RateBps(Booked, Appraised);

    /// Ordered by appraiser name, so the report reads the same way twice.
    public IReadOnlyList<LookToBookRow> Rows { get; init; } = [];
}

/// One recon category measured across every worksheet in the store.
public sealed record ReconVarianceReportRow
{
    public string Category { get; init; } = string.Empty;

    public int LineCount { get; init; }

    public long Estimate { get; init; }

    public long Actual { get; init; }

    /// `actual - estimate`; positive means the category ran over.
    public long Variance { get; init; }

    /// Lines in this category nothing has posted against yet. While this is
    /// above zero the variance is unfinished recon, not money saved.
    public int UnpostedLines { get; init; }
}

/// Estimate against actual for every recon category — the store-wide reading
/// of what `GET /api/appraisals/{id}/recon-variance` serves for one car.
public sealed record ReconVarianceReport
{
    public long TotalEstimate { get; init; }

    public long TotalActual { get; init; }

    public long TotalVariance { get; init; }

    public int UnpostedLines { get; init; }

    /// Ordered by category name, matching the per-worksheet rollup so the two
    /// views of the same numbers list them in the same order.
    public IReadOnlyList<ReconVarianceReportRow> Rows { get; init; } = [];
}

/// One appraiser's front gross across the worksheets the store won.
///
/// `TargetGross` is the gross those worksheets PLANNED — dealdesk has no
/// retail selling price to subtract a cost from, so the planned number is the
/// honest one — and `ProjectedGross` is that plan less what recon ran over.
public sealed record FrontGrossRow
{
    public string Appraiser { get; init; } = string.Empty;

    public int WonCount { get; init; }

    public long TargetGross { get; init; }

    /// Recon `actual - estimate` summed over this appraiser's won worksheets.
    /// Positive means recon ran over, which is why it is SUBTRACTED below.
    public long ReconVariance { get; init; }

    /// `targetGross - reconVariance`.
    public long ProjectedGross { get; init; }

    /// Recon lines on those worksheets with nothing posted against them.
    /// Above zero, the projection is provisional.
    public int UnpostedLines { get; init; }
}

/// Front gross by appraiser — SPEC.md feature 5's third report.
public sealed record FrontGrossReport
{
    public int WonCount { get; init; }

    public long TotalTargetGross { get; init; }

    public long TotalReconVariance { get; init; }

    public long TotalProjectedGross { get; init; }

    public int UnpostedLines { get; init; }

    /// Ordered by appraiser name.
    public IReadOnlyList<FrontGrossRow> Rows { get; init; } = [];
}
