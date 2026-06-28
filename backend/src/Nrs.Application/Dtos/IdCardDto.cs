using Nrs.Domain.Enums;

namespace Nrs.Application.Dtos;

/// <summary>
/// An ID card belonging to a person.
/// </summary>
public record IdCardDto
{
    /// <summary>Surrogate identifier of the ID card.</summary>
    public long IdCardId { get; init; }

    /// <summary>Civil Registration Number (CRN) of the owning person.</summary>
    public string CivilNumber { get; init; } = null!;

    /// <summary>The printed card number.</summary>
    public string CardNumber { get; init; } = null!;

    /// <summary>Issue date, if known.</summary>
    public DateOnly? IssueDate { get; init; }

    /// <summary>Expiry date, if known.</summary>
    public DateOnly? ExpiryDate { get; init; }

    /// <summary>Status of the card.</summary>
    public CardStatus Status { get; init; }

    /// <summary>Type/category of the card.</summary>
    public CardType CardType { get; init; }
}
