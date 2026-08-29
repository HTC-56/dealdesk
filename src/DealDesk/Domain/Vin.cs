namespace DealDesk.Domain;

/// A 17-character VIN checked by the public check-digit algorithm and nothing
/// else. dealdesk never decodes a VIN and never asks anyone about one —
/// SPEC.md's non-goals forbid outbound lookups — so all this type does is
/// arithmetic on characters.
///
/// The algorithm: transliterate each character to a number, multiply by that
/// position's weight, sum, take the remainder modulo 11. That remainder is the
/// ninth character (10 is written 'X'). The ninth position weighs 0, which is
/// why a check digit never influences its own sum.
public static class Vin
{
    /// Every VIN is exactly this long — the standard fixes it.
    public const int Length = 17;

    /// Zero-based index of the check digit: the ninth character.
    private const int CheckIndex = 8;

    private static readonly int[] Weights =
        [8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2];

    public static bool IsValid(string? candidate) => TryNormalize(candidate, out _);

    /// The candidate uppercased, once it is a legal 17 characters carrying its
    /// own correct check digit. Throws otherwise.
    public static string Normalize(string candidate)
    {
        if (!TryNormalize(candidate, out var normalized))
        {
            throw new ArgumentException("Not a valid VIN.", nameof(candidate));
        }

        return normalized;
    }

    /// True when the candidate is a legal 17 characters AND its ninth
    /// character is the check digit those characters imply.
    public static bool TryNormalize(string? candidate, out string normalized)
    {
        normalized = string.Empty;
        if (!TryPrepare(candidate, out var upper) || upper[CheckIndex] != Compute(upper))
        {
            return false;
        }

        normalized = upper;
        return true;
    }

    /// The character that belongs in the ninth position of this VIN. Cares
    /// only about the other sixteen, so it is safe to call on a candidate
    /// whose check digit is wrong or a placeholder.
    public static char CheckDigit(string candidate) => Compute(Prepared(candidate));

    /// The same VIN with its ninth character corrected. Demo and seed data
    /// builds plausible VINs this way instead of hand-guessing check digits.
    public static string WithCheckDigit(string candidate)
    {
        var upper = Prepared(candidate);
        var chars = upper.ToCharArray();
        chars[CheckIndex] = Compute(upper);
        return new string(chars);
    }

    private static string Prepared(string candidate) =>
        TryPrepare(candidate, out var upper)
            ? upper
            : throw new ArgumentException(
                "Not 17 characters of legal VIN characters.", nameof(candidate));

    /// Uppercases and checks the shape: right length, and every character one
    /// the standard allows. Says nothing about the check digit.
    private static bool TryPrepare(string? candidate, out string upper)
    {
        upper = string.Empty;
        if (candidate is null || candidate.Length != Length)
        {
            return false;
        }

        var uppercased = candidate.ToUpperInvariant();
        foreach (var character in uppercased)
        {
            if (Transliterate(character) < 0)
            {
                return false;
            }
        }

        upper = uppercased;
        return true;
    }

    private static char Compute(string upper)
    {
        var sum = 0;
        for (var i = 0; i < Length; i++)
        {
            sum += Transliterate(upper[i]) * Weights[i];
        }

        var remainder = sum % 11;
        return remainder == 10 ? 'X' : (char)('0' + remainder);
    }

    /// The numeric value of a VIN character, or -1 if the standard does not
    /// allow it. I, O and Q are excluded so they cannot be misread as 1 and 0.
    private static int Transliterate(char character) => character switch
    {
        >= '0' and <= '9' => character - '0',
        'A' or 'J' => 1,
        'B' or 'K' or 'S' => 2,
        'C' or 'L' or 'T' => 3,
        'D' or 'M' or 'U' => 4,
        'E' or 'N' or 'V' => 5,
        'F' or 'W' => 6,
        'G' or 'P' or 'X' => 7,
        'H' or 'Y' => 8,
        'R' or 'Z' => 9,
        _ => -1,
    };
}
