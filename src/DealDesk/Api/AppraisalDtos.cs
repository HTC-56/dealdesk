namespace DealDesk.Api;

/// An appraisal as the API serves it. Property names are the JSON field names;
/// the snake_case columns land on them through Dapper's underscore matching,
/// so the queries carry no AS aliases.
public sealed record AppraisalView
{
    public long Id { get; init; }

    public string Vin { get; init; } = string.Empty;

    public int ModelYear { get; init; }

    public string Make { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public string TrimLevel { get; init; } = string.Empty;

    public int Miles { get; init; }

    public string Appraiser { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string CreatedAt { get; init; } = string.Empty;

    public string UpdatedAt { get; init; } = string.Empty;
}

/// The body POST /api/appraisals accepts. Every field is nullable because the
/// client controls the JSON — Validate() is the whole rule set, so the
/// endpoint's only job is to call it and turn a message into a 400.
public sealed record CreateAppraisal
{
    public string? Vin { get; init; }

    public int ModelYear { get; init; }

    public string? Make { get; init; }

    public string? Model { get; init; }

    public string? TrimLevel { get; init; }

    public int Miles { get; init; }

    public string? Appraiser { get; init; }

    /// Null when the body is acceptable; otherwise the one-line reason, ready
    /// to hand back as the error message.
    ///
    /// The VIN rule is a check-digit test, not a lookup: dealdesk never
    /// decodes a VIN (SPEC.md non-goals).
    public string? Validate()
    {
        // Fully qualified: this record has a Vin property of its own, which
        // would otherwise shadow the domain type inside this method.
        if (!Domain.Vin.IsValid(Vin))
        {
            return "vin must be 17 legal characters with a correct check digit";
        }

        if (ModelYear is < 1900 or > 2200)
        {
            return "modelYear must be between 1900 and 2200";
        }

        if (string.IsNullOrWhiteSpace(Make))
        {
            return "make is required";
        }

        if (string.IsNullOrWhiteSpace(Model))
        {
            return "model is required";
        }

        if (string.IsNullOrWhiteSpace(Appraiser))
        {
            return "appraiser is required";
        }

        return Miles < 0 ? "miles cannot be negative" : null;
    }

    /// The VIN in the canonical uppercase form the row stores. Only meaningful
    /// once Validate() has returned null.
    public string NormalizedVin() => Domain.Vin.Normalize(Vin!);
}
