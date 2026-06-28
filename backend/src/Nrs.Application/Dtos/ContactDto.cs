namespace Nrs.Application.Dtos;

/// <summary>A person's contact details.</summary>
public record ContactDto
{
    public string? Mobile { get; init; }

    public string? Email { get; init; }
}
