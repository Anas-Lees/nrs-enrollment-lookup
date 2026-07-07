#pragma warning disable CA1707 // External/stored token; persisted as a string.
namespace Nrs.Domain.Enums;

/// <summary>The kind of lookup that was audited.</summary>
public enum AuditAction
{
    SEARCH,
    VIEW_PROFILE,

    /// <summary>An operator changed a person's address and contact details.</summary>
    UPDATE_CONTACT,
}
#pragma warning restore CA1707
