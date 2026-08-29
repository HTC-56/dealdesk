namespace DealDesk.Domain;

/// The one piece of arithmetic dealdesk exists to get right:
///
///     market anchor − recon estimate − pack − target front gross
///       ⇒ recommended trade value
///
/// Generic, public-domain retail-automotive arithmetic; nothing proprietary
/// and nothing fetched from anywhere. Every subtraction becomes a labelled
/// DerivationLine, so the number and its justification are the same object.
///
/// Two invariants hold for every input this accepts, and the property tests
/// pin both: the derivation amounts sum exactly to Recommended, and
/// Recommended never exceeds anchor − recon.
public static class OfferMath
{
    /// The anchor the comps imply: their arithmetic mean, to the nearest cent,
    /// halves rounded away from zero.
    ///
    /// Hand-entered comps are the only market input dealdesk has — SPEC.md's
    /// non-goals forbid feeds, book values and scraping — so this is the whole
    /// of "what the market says".
    public static Money MarketAnchor(IReadOnlyList<Money> compPrices)
    {
        ArgumentNullException.ThrowIfNull(compPrices);

        if (compPrices.Count == 0)
        {
            throw new ArgumentException(
                "An anchor needs at least one comp.", nameof(compPrices));
        }

        var total = Money.Sum(compPrices).Cents;
        long count = compPrices.Count;

        // Integer division truncates toward zero; nudge by one cent when the
        // remainder is at least half a step, in whichever direction the total
        // points. Staying in integers keeps the anchor exact at the cent.
        var quotient = Math.DivRem(total, count, out var remainder);
        var nudge = Math.Abs(remainder) * 2 >= count ? Math.Sign(total) : 0;

        return new Money(quotient + nudge);
    }

    /// The recommended trade value and the derivation behind it.
    ///
    /// Rejects negative pack, negative target gross and negative recon lines:
    /// each would quietly turn a subtraction into an addition and break the
    /// "never exceeds anchor − recon" invariant. A recon credit belongs in the
    /// recon actuals of a later phase, not in an estimate line.
    public static Offer Recommend(OfferInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        RejectNegative(inputs.Pack, "Pack");
        RejectNegative(inputs.TargetFrontGross, "Target front gross");

        var recon = Money.Zero;
        foreach (var line in inputs.ReconEstimates)
        {
            RejectNegative(line, "A recon estimate line");
            recon += line;
        }

        var overridden = inputs.AnchorOverride is not null;
        var anchor = inputs.AnchorOverride ?? MarketAnchor(inputs.CompPrices);

        var derivation = new List<DerivationLine>(4);
        var running = Money.Zero;

        void Step(string label, Money amount)
        {
            running += amount;
            derivation.Add(new DerivationLine(label, amount, running));
        }

        Step(overridden ? "Market anchor (entered)" : "Market anchor (comp average)", anchor);
        Step("Less recon estimate", -recon);
        Step("Less pack", -inputs.Pack);
        Step("Less target front gross", -inputs.TargetFrontGross);

        return new Offer
        {
            Anchor = anchor,
            Recon = recon,
            Recommended = running,
            Derivation = derivation,
        };
    }

    private static void RejectNegative(Money value, string what)
    {
        if (value < Money.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, what + " cannot be negative.");
        }
    }
}
