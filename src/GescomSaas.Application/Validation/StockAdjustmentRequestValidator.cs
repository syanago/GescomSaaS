using FluentValidation;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Validation;

public sealed class StockAdjustmentRequestValidator : AbstractValidator<StockAdjustmentRequest>
{
    public StockAdjustmentRequestValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("L'article est obligatoire.");

        RuleFor(x => x.WarehouseId)
            .NotEmpty().WithMessage("Le depot est obligatoire.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0m).WithMessage("La quantite doit etre strictement positive.");

        RuleFor(x => x.MovementType)
            .Must(t => t is StockMovementType.AdjustmentIn or StockMovementType.AdjustmentOut)
            .WithMessage("Le type de mouvement doit etre un ajustement (AdjustmentIn ou AdjustmentOut).");

        RuleFor(x => x.UnitCost)
            .GreaterThanOrEqualTo(0m).WithMessage("Le cout unitaire ne peut pas etre negatif.");

        RuleFor(x => x.MovementDate)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("La date du mouvement ne peut pas etre dans le futur.");

        RuleFor(x => x.ReferenceNumber)
            .MaximumLength(80).WithMessage("La reference ne doit pas depasser 80 caracteres.");

        RuleFor(x => x.LotNumber)
            .MaximumLength(40).WithMessage("Le numero de lot ne doit pas depasser 40 caracteres.");

        RuleFor(x => x.SerialNumber)
            .MaximumLength(80).WithMessage("Le numero de serie ne doit pas depasser 80 caracteres.");
    }
}
