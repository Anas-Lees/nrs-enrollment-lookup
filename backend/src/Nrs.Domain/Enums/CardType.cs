namespace Nrs.Domain.Enums;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Type/category of an ID card. Persisted as a string token.
/// </summary>
public enum CardType
{
    OMANI,
    RESIDENT,
    GCC,
    INVESTOR
}

#pragma warning restore CA1707
