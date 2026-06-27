namespace Nrs.Domain.Enums;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Status of a passport. Persisted as a string token.
/// </summary>
public enum PassportStatus
{
    ACTIVE,
    EXPIRED,
    CANCELLED,
    LOST,
    STOLEN
}

#pragma warning restore CA1707
