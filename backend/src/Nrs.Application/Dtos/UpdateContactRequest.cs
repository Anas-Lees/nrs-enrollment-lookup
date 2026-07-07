namespace Nrs.Application.Dtos;

/// <summary>
/// Operator-supplied contact details for a person. Editable independently of the address, so an
/// operator can set a mobile/email without also having to fill in an address (and vice versa).
/// </summary>
public record UpdateContactRequest
{
    public string? Mobile { get; init; }

    public string? Email { get; init; }
}
