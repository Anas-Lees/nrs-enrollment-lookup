#pragma warning disable CA1707, CA1720 // External contract tokens persisted as strings.
namespace Nrs.Domain.Enums;

/// <summary>Marital status. Persisted as its string name.</summary>
public enum MaritalStatus
{
    SINGLE,
    MARRIED,
    DIVORCED,
    WIDOWED,
}
#pragma warning restore CA1707
