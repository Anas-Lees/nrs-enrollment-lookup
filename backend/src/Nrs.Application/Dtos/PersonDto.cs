namespace Nrs.Application.Dtos;

/// <summary>
/// Full applicant profile, including related documents. Extends the summary row.
/// </summary>
public record PersonDto : PersonSummaryDto
{
    /// <summary>Relative path to the person's photo, if available.</summary>
    public string? PhotoPath { get; init; }

    /// <summary>ID cards associated with the person.</summary>
    public IReadOnlyList<IdCardDto> IdCards { get; init; } = [];

    /// <summary>Passports associated with the person.</summary>
    public IReadOnlyList<PassportDto> Passports { get; init; } = [];
}
