using FluentValidation;
using Nrs.Application.Dtos;

namespace Nrs.Api.Features.Persons;

/// <summary>Validates a stand-alone contact update. Both fields optional but format-checked when present.</summary>
public sealed class UpdateContactRequestValidator : AbstractValidator<UpdateContactRequest>
{
    public UpdateContactRequestValidator()
    {
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
