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

    /// <summary>
    /// The enrollment that produced this card, when it was issued through the review workflow.
    /// Null for cards that predate the workflow (e.g. seeded data). Used by the card office to
    /// find the card's Camunda process instance while it is being produced/collected.
    /// </summary>
    public Guid? EnrollmentId { get; set; }

    public Person? Person { get; set; }
}
