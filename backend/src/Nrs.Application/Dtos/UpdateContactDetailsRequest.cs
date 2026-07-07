namespace Nrs.Application.Dtos;

/// <summary>
/// Operator-supplied residential address and contact details for a person. Used to fill in
/// (or correct) the fields that a freshly-registered applicant does not yet have — a new
/// enrollment creates the person record from the application form, which carries names and
/// biographic data but no address or contact.
/// </summary>
public record UpdateContactDetailsRequest
{
    /// <summary>Governorate (one of Oman's 11). Required.</summary>
    public string Governorate { get; init; } = null!;

    /// <summary>Wilayat within the governorate. Required.</summary>
    public string Wilayat { get; init; } = null!;

    public string? Village { get; init; }

    public string? Street { get; init; }

    public string? BuildingNumber { get; init; }

    public string? PostalCode { get; init; }

    /// <summary>Mobile number in international form, e.g. "+96891234567".</summary>
    public string? Mobile { get; init; }

    public string? Email { get; init; }
}
