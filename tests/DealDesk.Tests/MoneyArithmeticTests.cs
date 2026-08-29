using DealDesk.Domain;
using Xunit;

namespace DealDesk.Tests;

/// Additional Money laws that complement the property tests. Each law is one
/// [Fact], looping Gen cases with a distinct seed, asserting inside the loop.
public sealed class MoneyArithmeticTests
{
    [Fact]
    public void Subtraction_undoes_addition()
    {
        foreach (var (a, b) in Gen.CentPairs(seed: 1006))
        {
            Money x = new Money(a) + new Money(b);
            Assert.Equal(new Money(a), x - new Money(b));
        }
    }

    [Fact]
    public void Negating_twice_returns_original()
    {
        foreach (var cents in Gen.Cents(seed: 1007))
        {
            Money x = new(cents);
            Assert.Equal(x, -(-x));
        }
    }

    [Fact]
    public void Zero_is_identity_for_addition()
    {
        foreach (var cents in Gen.Cents(seed: 1008))
        {
            Money x = new(cents);
            Assert.Equal(x, x + Money.Zero);
            Assert.Equal(x, Money.Zero + x);
        }
    }

    [Fact]
    public void Comparisons_agree_with_raw_cents()
    {
        foreach (var (a, b) in Gen.CentPairs(seed: 1009))
        {
            Money x = new(a), y = new(b);
            Assert.Equal(a < b, x < y);
            Assert.Equal(a > b, x > y);
            Assert.Equal(a <= b, x <= y);
            Assert.Equal(a >= b, x >= y);
        }
    }

    [Fact]
    public void FromDollars_rejects_sub_cent_amounts()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Money.FromDollars(10.005m));
        Assert.Equal("dollars", ex.ParamName);
    }

    [Fact]
    public void Sum_of_empty_sequence_is_zero()
    {
        Money[] empty = [];
        Assert.Equal(Money.Zero, Money.Sum(empty));
    }
}
