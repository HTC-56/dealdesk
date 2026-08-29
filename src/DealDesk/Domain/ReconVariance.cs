namespace DealDesk.Domain;

/// One line of the recon estimate, as the variance math sees it.
public sealed record ReconEstimateLine
{
    public required long LineId { get; init; }

    public required string Category { get; init; }

    public required string Description { get; init; }

    public required Money Estimate { get; init; }
}

/// One posted actual cost, against the estimate line it answers to. Amount is
/// signed: a supplier credit posts negative.
public readonly record struct ReconPosting(long LineId, Money Amount);

/// What one estimate line was expected to cost, what it actually cost, and the
/// gap between them.
public sealed record ReconLineVariance
{
    public required long LineId { get; init; }

    public required string Category { get; init; }

    public required string Description { get; init; }

    public required Money Estimate { get; init; }

    /// Every posting against this line, summed.
    public required Money Actual { get; init; }

    /// How many postings that sum came from. Zero means nobody has spent
    /// anything against this line yet.
    public required int PostingCount { get; init; }

    /// Actual minus estimate: POSITIVE means the line ran over what was
    /// estimated, negative means it came in under. Computed rather than
    /// stored, so no caller can hand out a variance that disagrees with the
    /// two numbers beside it.
    public Money Variance => Actual - Estimate;

    /// A line with no postings has a variance of −estimate, which is not the
    /// same claim as "came in under budget" — this flag is how a reader tells
    /// the two apart.
    public bool Posted => PostingCount > 0;
}

/// The same arithmetic rolled up to one recon category. SPEC.md feature 5's
/// recon-variance report groups by category, which is why the category
/// vocabulary is a CHECK constraint rather than free text.
public sealed record ReconCategoryVariance
{
    public required string Category { get; init; }

    public required Money Estimate { get; init; }

    public required Money Actual { get; init; }

    public required int LineCount { get; init; }

    public Money Variance => Actual - Estimate;
}

/// One worksheet's recon estimate against its recon actuals, line by line and
/// rolled up. Every total is derived from Lines on read, so a summary cannot
/// carry a total that disagrees with the lines it was built from.
public sealed record ReconVarianceSummary
{
    public required IReadOnlyList<ReconLineVariance> Lines { get; init; }

    /// Ordered by category name, so two worksheets with the same categories
    /// report them in the same order however their lines were entered.
    public required IReadOnlyList<ReconCategoryVariance> ByCategory { get; init; }

    public Money TotalEstimate => Money.Sum(Lines.Select(line => line.Estimate));

    public Money TotalActual => Money.Sum(Lines.Select(line => line.Actual));

    public Money TotalVariance => TotalActual - TotalEstimate;

    /// Lines nothing has been spent against yet. A worksheet still carrying
    /// unposted lines has not finished recon, so its total variance is not yet
    /// the final word.
    public int UnpostedLines => Lines.Count(line => !line.Posted);
}

/// The recon variance arithmetic — SPEC.md feature 4.
///
///     actual − estimate ⇒ variance, positive when the line ran over
///
/// The sign convention is the one a used-car director reads out loud: "we are
/// four hundred over on paint". It is fixed here, once, so the per-line
/// numbers, the category rollup and the worksheet total can never disagree
/// about which direction is bad.
///
/// Pure arithmetic over rows someone else read: nothing here opens a
/// connection, and nothing here rounds — every value is already whole cents.
public static class ReconVariance
{
    /// Pairs each estimate line with the postings made against it.
    ///
    /// Lines come back in the order they were given, which is the order the
    /// desk entered them. A posting naming a line that is not in the list is
    /// rejected rather than dropped: it would silently vanish from the totals,
    /// and money that vanishes from a variance report is the one failure this
    /// feature exists to prevent.
    public static ReconVarianceSummary Summarise(
        IReadOnlyList<ReconEstimateLine> lines,
        IReadOnlyList<ReconPosting> postings)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(postings);

        var posted = new Dictionary<long, (Money Amount, int Count)>();
        foreach (var posting in postings)
        {
            posted.TryGetValue(posting.LineId, out var running);
            posted[posting.LineId] = (running.Amount + posting.Amount, running.Count + 1);
        }

        var summarised = new List<ReconLineVariance>(lines.Count);
        foreach (var line in lines)
        {
            posted.TryGetValue(line.LineId, out var totals);
            posted.Remove(line.LineId);

            summarised.Add(new ReconLineVariance
            {
                LineId = line.LineId,
                Category = line.Category,
                Description = line.Description,
                Estimate = line.Estimate,
                Actual = totals.Amount,
                PostingCount = totals.Count,
            });
        }

        if (posted.Count > 0)
        {
            throw new ArgumentException(
                "A posting names recon line " + posted.Keys.First()
                + ", which is not one of the estimate lines given.",
                nameof(postings));
        }

        return new ReconVarianceSummary
        {
            Lines = summarised,
            ByCategory = Rollup(summarised),
        };
    }

    private static List<ReconCategoryVariance> Rollup(List<ReconLineVariance> lines)
    {
        var byCategory = new List<ReconCategoryVariance>();

        foreach (var group in lines
            .GroupBy(line => line.Category, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            byCategory.Add(new ReconCategoryVariance
            {
                Category = group.Key,
                Estimate = Money.Sum(group.Select(line => line.Estimate)),
                Actual = Money.Sum(group.Select(line => line.Actual)),
                LineCount = group.Count(),
            });
        }

        return byCategory;
    }
}
