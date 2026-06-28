using Nrs.Domain.Enums;

namespace Nrs.Application.Dtos;

/// <summary>
/// A passport belonging to a person.
/// </summary>
public record PassportDto
{
    /// <summary>Surrogate identifier of the passport.</summary>
    public long PassportId { get; init; }

    /// <summary>Civil Registration Number (CRN) of the owning person.</summary>
    public string CivilNumber { get; init; } = null!;

    /// <summary>The passport number.</summary>
    public string PassportNumber { get; init; } = null!;

    /// <summary>Type/category of the passport.</summary>
    public PassportType PassportType { get; init; }

    /// <summary>Issue date, if known.</summary>
    public DateOnly? IssueDate { get; init; }

    /// <summary>Expiry date, if known.</summary>
    public DateOnly? ExpiryDate { get; init; }

    /// <summary>Status of the passport.</summary>
    public PassportStatus Status { get; init; }
}
