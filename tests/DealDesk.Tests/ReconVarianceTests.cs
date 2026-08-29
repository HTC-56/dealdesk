using System;
using System.Collections.Generic;
using DealDesk.Domain;
using Xunit;

namespace DealDesk.Tests;

/// Recon variance arithmetic laws. Each law is one [Fact], plain Assert calls,
/// no database and no HTTP — ReconVariance is pure arithmetic.
public sealed class ReconVarianceTests
{
    private static List<ReconEstimateLine> FixtureLines()
    {
        return new List<ReconEstimateLine>
        {
            new() { LineId = 1, Category = "paint", Description = "respray rear quarter", Estimate = new Money(120_000) },
            new() { LineId = 2, Category = "tires", Description = "four 225/45R17", Estimate = new Money(64_000) },
            new() { LineId = 3, Category = "paint", Description = "clear coat second pass", Estimate = new Money(35_000) },
        };
    }

    private static List<ReconPosting> FixturePostings()
    {
        return new List<ReconPosting>
        {
            new(1, new Money(90_000)),
            new(1, new Money(45_000)),
            new(2, new Money(60_000)),
            new(2, new Money(-5_000)),
        };
    }

    [Fact]
    public void Line_1_has_Actual_135_000_Variance_15_000_PostingCount_2()
    {
        var summary = ReconVariance.Summarise(FixtureLines(), FixturePostings());
        var line1 = summary.Lines[0];

        Assert.Equal(new Money(135_000), line1.Actual);
        Assert.Equal(new Money(15_000), line1.Variance);
        Assert.Equal(2, line1.PostingCount);
    }

    [Fact]
    public void Line_2_has_Actual_55_000_Variance_minus_9_000()
    {
        var summary = ReconVariance.Summarise(FixtureLines(), FixturePostings());
        var line2 = summary.Lines[1];

        Assert.Equal(new Money(55_000), line2.Actual);
        Assert.Equal(new Money(-9_000), line2.Variance);
    }

    [Fact]
    public void Line_3_has_no_postings_Actual_0_Posted_false()
    {
        var summary = ReconVariance.Summarise(FixtureLines(), FixturePostings());
        var line3 = summary.Lines[2];

        Assert.Equal(Money.Zero, line3.Actual);
        Assert.Equal(new Money(-35_000), line3.Variance);
        Assert.False(line3.Posted);
    }

    [Fact]
    public void Worksheet_totals_are_derived_from_lines()
    {
        var summary = ReconVariance.Summarise(FixtureLines(), FixturePostings());

        Assert.Equal(new Money(219_000), summary.TotalEstimate);
        Assert.Equal(new Money(190_000), summary.TotalActual);
        Assert.Equal(new Money(-29_000), summary.TotalVariance);
        Assert.Equal(1, summary.UnpostedLines);
    }

    [Fact]
    public void ByCategory_has_two_entries_in_name_order()
    {
        var summary = ReconVariance.Summarise(FixtureLines(), FixturePostings());

        Assert.Equal(2, summary.ByCategory.Count);
        Assert.Equal("paint", summary.ByCategory[0].Category);
        Assert.Equal(2, summary.ByCategory[0].LineCount);
        Assert.Equal(new Money(155_000), summary.ByCategory[0].Estimate);
        Assert.Equal(new Money(135_000), summary.ByCategory[0].Actual);
        Assert.Equal("tires", summary.ByCategory[1].Category);
    }

    [Fact]
    public void Empty_lists_return_empty_lines_and_zero_variance_and_unknown_line_throws()
    {
        var empty = ReconVariance.Summarise(new List<ReconEstimateLine>(), new List<ReconPosting>());
        Assert.Empty(empty.Lines);
        Assert.Equal(Money.Zero, empty.TotalVariance);

        var lines = FixtureLines();
        var badPosting = new List<ReconPosting> { new(42, new Money(10_000)) };
        Assert.Throws<ArgumentException>(() => ReconVariance.Summarise(lines, badPosting));
    }
}
