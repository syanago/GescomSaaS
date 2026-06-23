using FluentValidation;
using GescomSaas.Application.Models;

namespace GescomSaas.Application.Validation;

public sealed class PaymentRegistrationRequestValidator : AbstractValidator<PaymentRegistrationRequest>
{
    public PaymentRegistrationRequestValidator()
    {
        RuleFor(x => x.PartnerId)
            .NotEmpty().WithMessage("Le tiers est obligatoire.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m).WithMessage("Le montant du reglement doit etre strictement positif.");

        RuleFor(x => x.PaymentDate)
            .Must(d => d >= new DateOnly(2000, 1, 1))
            .WithMessage("La date du reglement est invalide.")
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("La date du reglement ne peut pas etre dans le futur.");

        RuleFor(x => x.ReferenceNumber)
            .MaximumLength(80).WithMessage("La reference ne doit pas depasser 80 caracteres.");

        RuleFor(x => x.Notes)
            .MaximumLength(500).WithMessage("Les notes ne doivent pas depasser 500 caracteres.");

        // Note : la coherence cross-fields (DocumentId / AllocationMode / Type) est
        // verifiee dans SettlementService car elle depend de regles metier qui
        // demandent l'acces aux donnees (existence du document, type, etc.).
    }
}
