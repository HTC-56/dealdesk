using DealDesk.Domain;
using Xunit;

namespace DealDesk.Tests;

/// Offer-math property laws. Each law is one [Fact], looping Gen cases with
/// its own seed, asserting inside the loop. Gen yields negative cents, so
/// every generated value is wrapped in Math.Abs before becoming Pack,
/// TargetFrontGross, a recon line, or an AnchorOverride.
public sealed class OfferMathTests
{
    [Fact]
    public void Derivation_amounts_sum_to_Recommended()
    {
        // The invariant the worksheet depends on: every line's Amount added
        // back equals the final Recommended, exactly.
        foreach (var (a, b, c) in Gen.CentTriples(seed: 2001))
        {
            var inputs = new OfferInputs
            {
                AnchorOverride = new Money(Math.Abs(a)),
                ReconEstimates = [new Money(Math.Abs(b)), new Money(Math.Abs(c))],
                Pack = new Money(Math.Abs(a + b)),
                TargetFrontGross = new Money(Math.Abs(c)),
            };

            var offer = OfferMath.Recommend(inputs);

            Assert.Equal(offer.Recommended, Money.Sum(offer.Derivation.Select(l => l.Amount)));
        }
    }

    [Fact]
    public void Running_sum_equals_each_line_RunningTotal()
    {
        foreach (var (a, b, c) in Gen.CentTriples(seed: 2002))
        {
            var inputs = new OfferInputs
            {
                AnchorOverride = new Money(Math.Abs(a)),
                ReconEstimates = [new Money(Math.Abs(b)), new Money(Math.Abs(c))],
                Pack = new Money(Math.Abs(a + b)),
                TargetFrontGross = new Money(Math.Abs(c)),
            };

            var offer = OfferMath.Recommend(inputs);

            var running = Money.Zero;
            foreach (var line in offer.Derivation)
            {
                running += line.Amount;
                Assert.Equal(line.RunningTotal, running);
            }
        }
    }

    [Fact]
    public void Recommended_is_at_most_anchor_minus_recon()
    {
        foreach (var (a, b, c) in Gen.CentTriples(seed: 2003))
        {
            var inputs = new OfferInputs
            {
                AnchorOverride = new Money(Math.Abs(a)),
                ReconEstimates = [new Money(Math.Abs(b))],
                Pack = new Money(Math.Abs(c)),
                TargetFrontGross = new Money(Math.Abs(a)),
            };

            var offer = OfferMath.Recommend(inputs);

            Assert.True(offer.Recommended.Cents <= offer.Anchor.Cents - offer.Recon.Cents);
        }
    }

    [Fact]
    public void Derivation_has_exactly_four_lines_and_first_equals_anchor()
    {
        foreach (var (a, b, c) in Gen.CentTriples(seed: 2004))
        {
            var inputs = new OfferInputs
            {
                AnchorOverride = new Money(Math.Abs(a)),
                ReconEstimates = [new Money(Math.Abs(b))],
                Pack = new Money(Math.Abs(c)),
                TargetFrontGross = new Money(Math.Abs(a)),
            };

            var offer = OfferMath.Recommend(inputs);

            Assert.Equal(4, offer.Derivation.Count);
            Assert.Equal(offer.Anchor, offer.Derivation[0].Amount);
        }
    }

    [Fact]
    public void Market_anchor_halves_away_from_zero()
    {
        // Plain asserts, no loop: five fixed cases (values in cents).
        Assert.Equal(100, OfferMath.MarketAnchor([new Money(100)]).Cents);

        Assert.Equal(150, OfferMath.MarketAnchor([new Money(100), new Money(200)]).Cents);

        Assert.Equal(101, OfferMath.MarketAnchor([new Money(100), new Money(101)]).Cents);

        Assert.Equal(1, OfferMath.MarketAnchor([new Money(1), new Money(1), new Money(2)]).Cents);

        Assert.Equal(2, OfferMath.MarketAnchor([new Money(1), new Money(2), new Money(2)]).Cents);
    }

    [Fact]
    public void Negative_inputs_and_default_refuse()
    {
        // Negative Pack.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OfferMath.Recommend(new OfferInputs { Pack = new Money(-1) }));

        // Negative TargetFrontGross.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OfferMath.Recommend(new OfferInputs { TargetFrontGross = new Money(-1) }));

        // Negative recon line.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OfferMath.Recommend(new OfferInputs
            {
                ReconEstimates = [new Money(-1)],
            }));

        // No comps, no anchor override — MarketAnchor needs at least one comp.
        Assert.Throws<ArgumentException>(() =>
            OfferMath.Recommend(new OfferInputs()));
    }
}
