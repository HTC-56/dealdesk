using DealDesk.Api;
using Xunit;

namespace DealDesk.Tests;

/// Rate arithmetic laws — basis points, truncation, zero-whole safety.
/// Each law is one [Fact], plain Assert calls, no database and no HTTP.
public sealed class ReportRateTests
{
    [Fact]
    public void RateBps_returns_hundredths_of_a_percent()
    {
        Assert.Equal(5000, Reports.RateBps(1, 2));
        Assert.Equal(2500, Reports.RateBps(1, 4));
    }

    [Fact]
    public void RateBps_handles_all_and_none()
    {
        Assert.Equal(10_000, Reports.RateBps(3, 3));
        Assert.Equal(0, Reports.RateBps(0, 7));
    }

    [Fact]
    public void RateBps_truncates_toward_zero_not_rounds()
    {
        // 1/3 = 33.333...% → 3333 bps, never 3334
        Assert.Equal(3333, Reports.RateBps(1, 3));
    }

    [Fact]
    public void RateBps_whole_of_zero_returns_zero_no_throw()
    {
        Assert.Equal(0, Reports.RateBps(5, 0));
    }

    [Fact]
    public void BookRateBps_uses_appraised_not_looked_as_denominator()
    {
        var row = new LookToBookRow { Booked = 1, Appraised = 2 };
        Assert.Equal(5000, row.BookRateBps);

        var widerRow = new LookToBookRow
        {
            Appraiser = "A. Whitfield",
            Looked = 4,
            Appraised = 2,
            Booked = 1,
            Lost = 2,
            OpenWorksheets = 1,
        };
        // Looked=4 is irrelevant; denominator is Appraised=2
        Assert.Equal(5000, widerRow.BookRateBps);
    }

    [Fact]
    public void LookToBookReport_computed_rate_and_defaults()
    {
        var report = new LookToBookReport { Booked = 3, Appraised = 4 };
        Assert.Equal(7500, report.BookRateBps);

        var empty = new LookToBookReport();
        Assert.Equal(0, empty.BookRateBps);
        Assert.Empty(empty.Rows);
    }
}
