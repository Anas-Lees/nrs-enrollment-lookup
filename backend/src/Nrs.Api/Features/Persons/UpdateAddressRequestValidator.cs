using FluentValidation;
using Nrs.Application.Dtos;

namespace Nrs.Api.Features.Persons;

/// <summary>Validates a stand-alone address update — same rules as the address half of the combined form.</summary>
public sealed class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    public UpdateAddressRequestValidator()
    {
        RuleFor(r => r.Governorate)
            .NotEmpty().WithMessage("Governorate is required.")
            .Must(UpdateContactDetailsRequestValidator.Governorates.Contains)
            .WithMessage("Governorate must be one of Oman's 11 governorates.");

        RuleFor(r => r.Wilayat).NotEmpty().WithMessage("Wilayat is required.").MaximumLength(50);
        RuleFor(r => r.Village).MaximumLength(80);
        RuleFor(r => r.Street).MaximumLength(120);
        RuleFor(r => r.BuildingNumber).MaximumLength(20);
        RuleFor(r => r.PostalCode)
            .MaximumLength(10)
            .Matches("^[0-9]{3,10}$").When(r => !string.IsNullOrWhiteSpace(r.PostalCode))
            .WithMessage("Postal code must be 3–10 digits.");
    }
}
