namespace Nrs.Domain.Enums;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Status of an ID card. Persisted as a string token. A newly issued card runs through
/// production (IN_PRODUCTION → READY_FOR_COLLECTION → ACTIVE) before it becomes a live card.
/// A person holds at most one live card: issuing a new one supersedes the previous card.
/// </summary>
public enum CardStatus
{
    /// <summary>Approved and being produced/printed by the card office.</summary>
    IN_PRODUCTION,

    /// <summary>Printed and waiting for the applicant to collect it.</summary>
    READY_FOR_COLLECTION,

    ACTIVE,
    EXPIRED,

    /// <summary>Invalidated because a newer card was issued to the same holder (renewal/replacement).</summary>
    SUPERSEDED,

    BLOCKED,
    LOST
}

#pragma warning restore CA1707
