using Nrs.Domain.Enums;

namespace Nrs.Domain.Entities;

/// <summary>
/// A passport belonging to a person. Maps to the PASSPORT table.
/// </summary>
public class Passport
{
    /// <summary>Primary key (auto-increment).</summary>
    public long PassportId { get; set; }

    /// <summary>Foreign key to <see cref="Person"/>.</summary>
    public string CivilNumber { get; set; } = null!;

    public string PassportNumber { get; set; } = null!;

    public PassportType PassportType { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public PassportStatus Status { get; set; }

    public Person? Person { get; set; }
}
