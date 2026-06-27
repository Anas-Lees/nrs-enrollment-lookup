namespace Nrs.Domain.Enums;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// Lifecycle status of a person record. Persisted as a string token.
/// </summary>
public enum PersonStatus
{
    ACTIVE,
    DECEASED,
    MERGED
}

#pragma warning restore CA1707
