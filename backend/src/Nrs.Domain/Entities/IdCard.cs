using Nrs.Domain.Enums;

namespace Nrs.Domain.Entities;

/// <summary>
/// An identity card belonging to a person. Maps to the ID_CARD table.
/// </summary>
public class IdCard
{
    /// <summary>Primary key (auto-increment).</summary>
    public long IdCardId { get; set; }

    /// <summary>Foreign key to <see cref="Person"/>.</summary>
    public string CivilNumber { get; set; } = null!;

    public string CardNumber { get; set; } = null!;

    public DateOnly? IssueDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public CardStatus Status { get; set; }

    public CardType CardType { get; set; }

    public Person? Person { get; set; }
}
