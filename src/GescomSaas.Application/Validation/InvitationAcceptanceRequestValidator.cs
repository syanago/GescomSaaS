using FluentValidation;
using GescomSaas.Application.Models;

namespace GescomSaas.Application.Validation;

public sealed class InvitationAcceptanceRequestValidator : AbstractValidator<InvitationAcceptanceRequest>
{
    public InvitationAcceptanceRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Le prenom est obligatoire.")
            .MaximumLength(80).WithMessage("Le prenom ne doit pas depasser 80 caracteres.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Le nom est obligatoire.")
            .MaximumLength(80).WithMessage("Le nom ne doit pas depasser 80 caracteres.");

        // Le mot de passe est optionnel ici (cas du compte existant ASP.NET Identity)
        // mais s'il est fourni il doit respecter les regles minimales.
        // La regle definitive est appliquee par UserManager.CreateAsync, donc on
        // se contente d'un garde-fou de surface (longueur) pour eviter les inputs
        // pathologiques avant d'appeler Identity.
        RuleFor(x => x.Password!)
            .MinimumLength(6).WithMessage("Le mot de passe doit contenir au moins 6 caracteres.")
            .MaximumLength(128).WithMessage("Le mot de passe ne doit pas depasser 128 caracteres.")
            .When(x => !string.IsNullOrEmpty(x.Password));
    }
}
