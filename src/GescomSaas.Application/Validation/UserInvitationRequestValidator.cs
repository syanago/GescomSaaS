using FluentValidation;
using GescomSaas.Application.Models;

namespace GescomSaas.Application.Validation;

public sealed class UserInvitationRequestValidator : AbstractValidator<UserInvitationRequest>
{
    public UserInvitationRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("L'adresse e-mail est obligatoire.")
            .EmailAddress().WithMessage("Format d'adresse e-mail invalide.")
            .MaximumLength(256).WithMessage("L'adresse e-mail ne doit pas depasser 256 caracteres.");

        RuleFor(x => x.FirstName)
            .MaximumLength(80).WithMessage("Le prenom ne doit pas depasser 80 caracteres.");

        RuleFor(x => x.LastName)
            .MaximumLength(80).WithMessage("Le nom ne doit pas depasser 80 caracteres.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Les notes ne doivent pas depasser 500 caracteres.");

        RuleFor(x => x.Roles)
            .NotNull().WithMessage("La liste des roles est obligatoire.")
            .Must(r => r is not null && r.Count > 0)
            .WithMessage("Selectionnez au moins un role.")
            .Must(r => r is null || r.All(role => !string.IsNullOrWhiteSpace(role)))
            .WithMessage("Les roles ne peuvent pas contenir de valeurs vides.");
    }
}
