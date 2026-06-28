namespace Nrs.Application.Dtos;

/// <summary>A person's current residential address.</summary>
public record AddressDto
{
    public string Governorate { get; init; } = null!;

    public string Wilayat { get; init; } = null!;

    public string? Village { get; init; }

    public string? Street { get; init; }

    public string? BuildingNumber { get; init; }

    public string? PostalCode { get; init; }
}
