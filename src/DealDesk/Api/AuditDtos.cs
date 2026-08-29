using DealDesk.Domain;

namespace DealDesk.Api;

/// The audit trail as the API serves it, and the two bodies that write to it.
///
/// Three rules hold across this file:
///
/// * Every write that changes a value carries `changedBy` and `reason`. There
///   is no user system in v1 (SPEC.md non-goals), so who-did-it is a name the
///   caller supplies, exactly like the `appraiser` field on the worksheet. Both
///   are required: an untraceable change is the thing this feature exists to
///   prevent, and the CHECK constraints in sql/003_audit.sql refuse either one
///   blank anyway.
/// * Old and new values travel as strings, because one trail carries a status
///   word, an odometer reading and a person's name.
/// * Validate() returns null when the body is acceptable and a one-line reason
///   otherwise — the same contract CreateAppraisal uses, so an endpoint's whole
///   job is turning that message into a 400.

/// One recorded change: who moved which field, from what to what, when, and why.
public sealed record AuditEntryView
{
    public long Id { get; init; }

    public long AppraisalId { get; init; }

    public string Field { get; init; } = string.Empty;

    public string OldValue { get; init; } = string.Empty;

    public string NewValue { get; init; } = string.Empty;

    public string ChangedBy { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public string ChangedAt { get; init; } = string.Empty;
}

/// The body POST /api/appraisals/{id}/status accepts. Whether the move itself
/// is legal is Lifecycle's question, not this one — Validate() only rules on
/// the shape of the request, so an unknown word is a 400 and a real-but-illegal
/// move is a 409.
public sealed record ChangeStatus
{
    public string? Status { get; init; }

    public string? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public string? Validate()
    {
        if (!Lifecycle.IsStatus(Status))
        {
            return "status must be one of: " + string.Join(", ", Lifecycle.All);
        }

        if (string.IsNullOrWhiteSpace(ChangedBy))
        {
            return "changedBy is required — every change records who made it";
        }

        return string.IsNullOrWhiteSpace(Reason)
            ? "reason is required — every change records why it was made"
            : null;
    }

    public string CanonicalStatus() => Lifecycle.Canonical(Status!);
}

/// The body PATCH /api/appraisals/{id} accepts: the worksheet fields the desk
/// may revise after the row exists, plus the two audit fields.
///
/// Every value field is nullable and null means "leave it alone" — a PATCH
/// carrying only `miles` touches only miles. `vin` is absent on purpose: the
/// VIN is the vehicle's identity, and correcting it means the worksheet was
/// opened on the wrong car. `status` is absent too — it moves through the
/// lifecycle route, which checks the transition.
public sealed record ReviseAppraisal
{
    public int? ModelYear { get; init; }

    public string? Make { get; init; }

    public string? Model { get; init; }

    public string? TrimLevel { get; init; }

    public int? Miles { get; init; }

    public string? Appraiser { get; init; }

    public string? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public string? Validate()
    {
        if (!TouchesAnything())
        {
            return "supply at least one of: modelYear, make, model, trimLevel, miles, appraiser";
        }

        if (ModelYear is < 1900 or > 2200)
        {
            return "modelYear must be between 1900 and 2200";
        }

        if (Miles < 0)
        {
            return "miles cannot be negative";
        }

        // A blank name or model would erase the field rather than revise it.
        if (Make is not null && string.IsNullOrWhiteSpace(Make))
        {
            return "make cannot be blank";
        }

        if (Model is not null && string.IsNullOrWhiteSpace(Model))
        {
            return "model cannot be blank";
        }

        if (Appraiser is not null && string.IsNullOrWhiteSpace(Appraiser))
        {
            return "appraiser cannot be blank";
        }

        if (string.IsNullOrWhiteSpace(ChangedBy))
        {
            return "changedBy is required — every change records who made it";
        }

        return string.IsNullOrWhiteSpace(Reason)
            ? "reason is required — every change records why it was made"
            : null;
    }

    /// True when the body names at least one field to revise. A PATCH that
    /// carries only `changedBy` and `reason` asks for nothing.
    public bool TouchesAnything() =>
        ModelYear is not null || Make is not null || Model is not null
        || TrimLevel is not null || Miles is not null || Appraiser is not null;
}
