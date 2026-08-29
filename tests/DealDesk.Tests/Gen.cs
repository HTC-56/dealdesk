namespace DealDesk.Tests;

/// Deterministic case generation for the property tests.
///
/// dealdesk deliberately carries no property-testing package — the dependency
/// surface is pre-registered — so properties are xUnit facts that loop over
/// seeded cases. A fixed seed means a red test is reproducible from the seed
/// in its name, which is the only part of FsCheck we actually need.
///
/// Every generator yields the boundary values FIRST, then random ones, so a
/// property that only breaks at zero still fails on case 1.
internal static class Gen
{
    /// Cases per property. Big enough to catch sign and carry bugs, small
    /// enough that the whole suite stays sub-second.
    public const int Cases = 250;

    /// Bounded so that summing three generated values cannot overflow long,
    /// and wide enough to cover any plausible vehicle money value.
    public const long MaxAbsCents = 5_000_000_00;

    private static readonly long[] Boundaries = [0L, 1L, -1L, 99L, 100L, -100L, MaxAbsCents, -MaxAbsCents];

    public static IEnumerable<long> Cents(int seed, int count = Cases)
    {
        foreach (var boundary in Boundaries)
        {
            yield return boundary;
        }

        var random = new Random(seed);
        for (var i = Boundaries.Length; i < count; i++)
        {
            yield return random.NextInt64(-MaxAbsCents, MaxAbsCents + 1);
        }
    }

    public static IEnumerable<(long A, long B)> CentPairs(int seed, int count = Cases)
    {
        var random = new Random(seed);
        foreach (var a in Cents(seed, count))
        {
            yield return (a, random.NextInt64(-MaxAbsCents, MaxAbsCents + 1));
        }
    }

    public static IEnumerable<(long A, long B, long C)> CentTriples(int seed, int count = Cases)
    {
        var random = new Random(seed);
        foreach (var a in Cents(seed, count))
        {
            yield return (
                a,
                random.NextInt64(-MaxAbsCents, MaxAbsCents + 1),
                random.NextInt64(-MaxAbsCents, MaxAbsCents + 1));
        }
    }
}
