using FluentValidation;
using Nrs.Application.Dtos;

namespace Nrs.Api.Features.Persons;

/// <summary>
/// Validates operator-supplied address and contact details. The address columns are stored as
/// single-byte (Latin) text — matching the seeded records — so the governorate is constrained
/// to Oman's 11 governorates by their English names, and the free-text fields are bounded to
/// their column widths. Contact fields are optional but format-checked when present.
/// </summary>
public sealed class UpdateContactDetailsRequestValidator : AbstractValidator<UpdateContactDetailsRequest>
{
    /// <summary>The 11 governorates of Oman, by English name (the value stored in ADDRESS).</summary>
    public static readonly IReadOnlySet<string> Governorates = new HashSet<string>(StringComparer.Ordinal)
    {
        "Muscat",
        "Dhofar",
        "Musandam",
        "Al Buraimi",
        "Ad Dakhiliyah",
        "Al Batinah North",
        "Al Batinah South",
        "Ash Sharqiyah North",
        "Ash Sharqiyah South",
        "Adh Dhahirah",
        "Al Wusta",
    };

    public UpdateContactDetailsRequestValidator()
    {
        RuleFor(r => r.Governorate)
            .NotEmpty().WithMessage("Governorate is required.")
            .Must(Governorates.Contains).WithMessage("Governorate must be one of Oman's 11 governorates.");

        RuleFor(r => r.Wilayat)
            .NotEmpty().WithMessage("Wilayat is required.")
            .MaximumLength(50);

        RuleFor(r => r.Village).MaximumLength(80);
        RuleFor(r => r.Street).MaximumLength(120);
        RuleFor(r => r.BuildingNumber).MaximumLength(20);

        RuleFor(r => r.PostalCode)
            .MaximumLength(10)
            .Matches("^[0-9]{3,10}$").When(r => !string.IsNullOrWhiteSpace(r.PostalCode))
            .WithMessage("Postal code must be 3–10 digits.");

        RuleFor(r => r.Mobile)
            .MaximumLength(20)
            .Matches(@"^\+?[0-9][0-9\s]{6,18}$").When(r => !string.IsNullOrWhiteSpace(r.Mobile))
            .WithMessage("Enter a valid mobile number, e.g. +96891234567.");

        RuleFor(r => r.Email)
            .MaximumLength(120)
            .EmailAddress().When(r => !string.IsNullOrWhiteSpace(r.Email))
            .WithMessage("Enter a valid email address.");
    }
}
