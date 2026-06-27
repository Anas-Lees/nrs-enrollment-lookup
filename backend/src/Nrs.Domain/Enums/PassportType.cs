namespace Nrs.Domain.Enums;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Type/category of a passport. Persisted as a string token.
/// </summary>
public enum PassportType
{
    ORDINARY,
    DIPLOMATIC,
    SERVICE,
    SPECIAL,
    ROYAL_DIPLOMATIC
}

#pragma warning restore CA1707
