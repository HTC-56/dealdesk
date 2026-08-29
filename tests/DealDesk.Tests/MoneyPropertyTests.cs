using DealDesk.Domain;
using Xunit;

namespace DealDesk.Tests;

/// Properties of the money type the offer math will stand on. These are the
/// pattern for every later property test: loop Gen cases, assert the law,
/// report the failing case in the assertion message.
public sealed class MoneyPropertyTests
{
    [Fact]
    public void Cents_round_trip_exactly()
    {
        foreach (var cents in Gen.Cents(seed: 1001))
        {
            Assert.Equal(cents, new Money(cents).Cents);
        }
    }

    [Fact]
    public void Addition_is_commutative()
    {
        foreach (var (a, b) in Gen.CentPairs(seed: 1002))
        {
            Assert.Equal(new Money(a) + new Money(b), new Money(b) + new Money(a));
        }
    }

    [Fact]
    public void Addition_is_associative()
    {
        foreach (var (a, b, c) in Gen.CentTriples(seed: 1003))
        {
            Money x = new(a), y = new(b), z = new(c);
            Assert.Equal((x + y) + z, x + (y + z));
        }
    }

    [Fact]
    public void Sum_of_parts_equals_the_whole()
    {
        // The invariant every derivation on the worksheet depends on: a total
        // split into line items and summed back is the same total, exactly.
        foreach (var (a, b, c) in Gen.CentTriples(seed: 1004))
        {
            Money[] parts = [new(a), new(b), new(c)];
            Assert.Equal(new Money(a) + new Money(b) + new Money(c), Money.Sum(parts));
        }
    }

    [Fact]
    public void Dollars_round_trip_through_cents()
    {
        foreach (var cents in Gen.Cents(seed: 1005))
        {
            var money = new Money(cents);
            Assert.Equal(money, Money.FromDollars(money.ToDollars()));
        }
    }
}
