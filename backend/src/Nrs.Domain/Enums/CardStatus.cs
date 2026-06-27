namespace Nrs.Domain.Enums;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Status of an ID card. Persisted as a string token.
/// </summary>
public enum CardStatus
{
    ACTIVE,
    EXPIRED,
    BLOCKED,
    LOST
}

#pragma warning restore CA1707
