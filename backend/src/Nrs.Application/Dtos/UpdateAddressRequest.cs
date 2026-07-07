namespace Nrs.Application.Dtos;

/// <summary>
/// Operator-supplied residential address for a person. Editable independently of contact, so an
/// operator can complete or correct just the address.
/// </summary>
public record UpdateAddressRequest
{
    /// <summary>Governorate (one of Oman's 11). Required.</summary>
    public string Governorate { get; init; } = null!;

    /// <summary>Wilayat within the governorate. Required.</summary>
    public string Wilayat { get; init; } = null!;

    public string? Village { get; init; }

    public string? Street { get; init; }

    public string? BuildingNumber { get; init; }

    public string? PostalCode { get; init; }
}
