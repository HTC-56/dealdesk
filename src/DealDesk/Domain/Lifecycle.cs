namespace DealDesk.Domain;

/// The worksheet lifecycle SPEC.md feature 3 names: draft → appraised →
/// presented → won | lost.
///
/// The chain moves forward one step at a time, and `lost` is reachable from
/// every open state — a deal can die before it is ever presented, and pretending
/// otherwise would force the desk to fake a presentation to close the file.
/// `won` is only reachable from `presented`: a vehicle nobody was shown an offer
/// on cannot have been bought. Both are terminal; a closed worksheet does not
/// reopen, because look-to-book counts closed files.
///
/// Standing still is not a transition. Moving `draft` → `draft` writes no audit
/// row, so it is refused rather than recorded as a change that did not happen.
///
/// This type holds no database and no HTTP: it answers whether a move is legal
/// and, when it is not, why. LifecycleEndpoints turns that answer into a 409.
public static class Lifecycle
{
    public const string Draft = "draft";

    public const string Appraised = "appraised";

    public const string Presented = "presented";

    public const string Won = "won";

    public const string Lost = "lost";

    /// Every legal status, in lifecycle order. Same list as the
    /// `appraisal_status_valid` CHECK constraint in sql/001_init.sql.
    public static readonly string[] All = [Draft, Appraised, Presented, Won, Lost];

    private static readonly Dictionary<string, string[]> Onward = new(StringComparer.Ordinal)
    {
        [Draft] = [Appraised, Lost],
        [Appraised] = [Presented, Lost],
        [Presented] = [Won, Lost],
        [Won] = [],
        [Lost] = [],
    };

    /// True when the trimmed, case-insensitive value is one of the five states.
    public static bool IsStatus(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Array.Exists(All, s => s.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));

    /// The stored form: trimmed and lowercase. Only meaningful for a value
    /// IsStatus has accepted.
    public static string Canonical(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Trim().ToLowerInvariant();
    }

    /// Where a worksheet in this state may go next — empty once it is closed.
    /// Throws for a status that is not one of the five.
    public static IReadOnlyList<string> NextFrom(string status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return Onward.TryGetValue(Canonical(status), out var onward)
            ? onward
            : throw new ArgumentOutOfRangeException(
                nameof(status), status, "not a worksheet status");
    }

    /// True when nothing follows this state: `won` and `lost`.
    public static bool IsTerminal(string status) => NextFrom(status).Count == 0;

    /// True when the move is one the lifecycle allows.
    public static bool CanMove(string from, string to) =>
        IsStatus(from) && IsStatus(to)
        && Array.Exists(Onward[Canonical(from)], s => s == Canonical(to));

    /// Null when the move is legal; otherwise the one-line reason, ready to hand
    /// back as the error message on a 409.
    public static string? Refuse(string from, string to)
    {
        if (!IsStatus(to))
        {
            return "status must be one of: " + string.Join(", ", All);
        }

        if (CanMove(from, to))
        {
            return null;
        }

        var current = Canonical(from);
        var wanted = Canonical(to);

        if (current == wanted)
        {
            return $"this worksheet is already {current}";
        }

        return IsTerminal(current)
            ? $"{current} is a closed worksheet and does not move again"
            : $"{current} cannot move to {wanted}; from {current} the worksheet goes to "
                + string.Join(" or ", Onward[current]);
    }
}
