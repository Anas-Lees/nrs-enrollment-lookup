namespace Nrs.Domain.Enums;

#pragma warning disable CA1707 // Identifiers should not contain underscores

/// <summary>
/// What an enrollment application is for. Persisted as its string name.
/// </summary>
public enum EnrollmentType
{
    /// <summary>First-time issuance of a national ID card.</summary>
    NEW_CARD,

    /// <summary>Renewal of an expiring or expired card.</summary>
    RENEWAL,

    /// <summary>Replacement for a lost, stolen or damaged card.</summary>
    REPLACEMENT,

    /// <summary>Correction of biographic data on an existing record.</summary>
    CORRECTION
}

#pragma warning restore CA1707
