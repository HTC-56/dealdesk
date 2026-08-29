using System.Globalization;

namespace DealDesk.Domain;

/// Currency as whole cents. Every money value in dealdesk is this type — the
/// offer math must sum exactly, and binary floating point cannot promise that.
/// Stored in SQLite as INTEGER cents (see MoneyTypeHandler).
public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static Money Zero => new(0);

    /// Parses a dollar amount. Rejects fractions of a cent rather than
    /// silently rounding them away.
    public static Money FromDollars(decimal dollars)
    {
        var cents = dollars * 100m;
        if (cents != decimal.Truncate(cents))
        {
            throw new ArgumentOutOfRangeException(
                nameof(dollars), dollars, "Amount is finer than one cent.");
        }

        return new Money((long)cents);
    }

    public decimal ToDollars() => Cents / 100m;

    public static Money Sum(IEnumerable<Money> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        long total = 0;
        foreach (var part in parts)
        {
            total = checked(total + part.Cents);
        }

        return new Money(total);
    }

    public static Money operator +(Money left, Money right) =>
        new(checked(left.Cents + right.Cents));

    public static Money operator -(Money left, Money right) =>
        new(checked(left.Cents - right.Cents));

    public static Money operator -(Money value) => new(checked(-value.Cents));

    public static bool operator <(Money left, Money right) => left.Cents < right.Cents;

    public static bool operator >(Money left, Money right) => left.Cents > right.Cents;

    public static bool operator <=(Money left, Money right) => left.Cents <= right.Cents;

    public static bool operator >=(Money left, Money right) => left.Cents >= right.Cents;

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

    /// Plain invariant "1234.56" / "-1234.56" — no currency symbol, no
    /// thousands separators, so it round-trips through JSON and SQL unchanged.
    public override string ToString() =>
        ToDollars().ToString("0.00", CultureInfo.InvariantCulture);
}
