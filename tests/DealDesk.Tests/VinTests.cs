using System;
using DealDesk.Domain;
using Xunit;

namespace DealDesk.Tests;

/// VIN validation laws. Each law is one [Fact], plain Assert calls, no loops
/// needed — the spec is a fixed 17-character string with a small alphabet.
public sealed class VinTests
{
    private const string ValidVin = "ZZ9ZZ99Z2Z9000042";
    private const string InvalidVin = "ZZ9ZZ99Z9Z9000042"; // wrong check digit

    [Fact]
    public void Valid_Vin_passes_IsValid_and_invalid_fails()
    {
        Assert.True(Vin.IsValid(ValidVin));
        Assert.False(Vin.IsValid(InvalidVin));
    }

    [Fact]
    public void Lowercase_input_is_normalized_to_uppercase()
    {
        var lower = ValidVin.ToLowerInvariant();
        Assert.True(Vin.IsValid(lower));
        Assert.Equal(ValidVin, Vin.Normalize(lower));
    }

    [Fact]
    public void Non_seventeen_and_null_are_rejected()
    {
        Assert.False(Vin.IsValid("ZZ9ZZ99Z2Z900004"));  // 16 chars
        Assert.False(Vin.IsValid("ZZ9ZZ99Z2Z9000042X")); // 18 chars
        Assert.False(Vin.IsValid(null));
    }

    [Fact]
    public void VINs_containing_I_O_or_Q_are_rejected()
    {
        Assert.False(Vin.IsValid("ZZ9ZZ99Z2Z9000I42"));
        Assert.False(Vin.IsValid("ZZ9ZZ99Z2Z9000O42"));
        Assert.False(Vin.IsValid("ZZ9ZZ99Z2Z9000Q42"));
    }

    [Fact]
    public void CheckDigit_computes_and_WithCheckDigit_repairs()
    {
        Assert.Equal('2', Vin.CheckDigit(InvalidVin));
        Assert.Equal(ValidVin, Vin.WithCheckDigit(InvalidVin));
    }

    [Fact]
    public void Normalize_throws_on_invalid_Vin()
    {
        var ex = Assert.Throws<ArgumentException>(() => Vin.Normalize(InvalidVin));
        Assert.Equal("candidate", ex.ParamName);
    }
}
