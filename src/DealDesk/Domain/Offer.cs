namespace DealDesk.Domain;

/// One step of the offer derivation: what the step is called, the signed
/// amount it applies, and the running total after applying it.
///
/// The worksheet renders these in order and shows nothing else — every number
/// on the page is either an Amount or a RunningTotal here. That is what
/// SPEC.md means by "no magic totals".
public readonly record struct DerivationLine(string Label, Money Amount, Money RunningTotal);

/// What the desk actually types on a worksheet: the comps, the recon estimate
/// lines, and the two store numbers.
public sealed record OfferInputs
{
    /// The hand-entered comparable prices. Their average is the anchor unless
    /// AnchorOverride says otherwise.
    public IReadOnlyList<Money> CompPrices { get; init; } = [];

    /// An anchor the desk typed instead of taking the comp average. Null means
    /// "use the comps".
    public Money? AnchorOverride { get; init; }

    /// The itemized recon estimate, one entry per line. Summed, never averaged.
    public IReadOnlyList<Money> ReconEstimates { get; init; } = [];

    /// The store's fixed cost per unit.
    public Money Pack { get; init; }

    /// The front gross the store intends to make on the resale.
    public Money TargetFrontGross { get; init; }
}

/// A recommended trade value together with the derivation that produced it.
/// The two always travel together — the API never returns one without the
/// other.
public sealed record Offer
{
    /// The market anchor used, whether averaged from comps or overridden.
    public required Money Anchor { get; init; }

    /// The recon estimate lines, summed.
    public required Money Recon { get; init; }

    /// What to put on the worksheet: the last RunningTotal in Derivation.
    public required Money Recommended { get; init; }

    /// Every step, in the order they apply.
    public required IReadOnlyList<DerivationLine> Derivation { get; init; }
}
