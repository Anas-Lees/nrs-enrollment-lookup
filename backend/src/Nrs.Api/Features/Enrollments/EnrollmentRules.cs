namespace Nrs.Api.Features.Enrollments;

/// <summary>
/// Small rules shared across the enrollment slices (reference-number generation, input
/// tidying, and the plausible date-of-birth check used by the create/edit validators).
/// </summary>
public static class EnrollmentRules
{
    /// <summary>Derives a short, human-friendly reference number from the enrollment id.</summary>
    public static string NewReferenceNumber(Guid id) =>
        "ENR-" + id.ToString("N")[..8].ToUpperInvariant();

    /// <summary>Trims a string and collapses empty/whitespace to null.</summary>
    public static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>A date of birth must be after 1900 and strictly in the past.</summary>
    public static bool BeAPlausibleDateOfBirth(DateOnly dob) =>
        dob > new DateOnly(1900, 1, 1) && dob < DateOnly.FromDateTime(DateTime.UtcNow);
}
