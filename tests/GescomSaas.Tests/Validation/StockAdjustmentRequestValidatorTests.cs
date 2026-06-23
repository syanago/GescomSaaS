using FluentValidation.TestHelper;
using GescomSaas.Application.Models;
using GescomSaas.Application.Validation;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Tests.Validation;

public class StockAdjustmentRequestValidatorTests
{
    private readonly StockAdjustmentRequestValidator _sut = new();

    private static StockAdjustmentRequest Valid() => new(
        ProductId: Guid.NewGuid(),
        WarehouseId: Guid.NewGuid(),
        MovementDate: DateOnly.FromDateTime(DateTime.UtcNow),
        MovementType: StockMovementType.AdjustmentIn,
        Quantity: 5m,
        UnitCost: 12.50m,
        ReferenceNumber: "INV-2026-001",
        LotNumber: null,
        SerialNumber: null,
        ExpirationDate: null);

    [Fact]
    public void RequetValide_PasseSansErreur()
    {
        var result = _sut.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void ProductId_Vide_Echoue()
    {
        var result = _sut.TestValidate(Valid() with { ProductId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.ProductId);
    }

    [Fact]
    public void WarehouseId_Vide_Echoue()
    {
        var result = _sut.TestValidate(Valid() with { WarehouseId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.WarehouseId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantite_NulleOuNegative_Echoue(decimal qty)
    {
        var result = _sut.TestValidate(Valid() with { Quantity = qty });
        result.ShouldHaveValidationErrorFor(x => x.Quantity);
    }

    [Theory]
    [InlineData(StockMovementType.Receipt)]
    [InlineData(StockMovementType.Issue)]
    [InlineData(StockMovementType.OpeningBalance)]
    public void TypeMouvement_NonAjustement_Echoue(StockMovementType type)
    {
        var result = _sut.TestValidate(Valid() with { MovementType = type });
        result.ShouldHaveValidationErrorFor(x => x.MovementType);
    }

    [Fact]
    public void DateMouvement_DansLeFutur_Echoue()
    {
        var futur = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var result = _sut.TestValidate(Valid() with { MovementDate = futur });
        result.ShouldHaveValidationErrorFor(x => x.MovementDate);
    }

    [Fact]
    public void CoutUnitaire_Negatif_Echoue()
    {
        var result = _sut.TestValidate(Valid() with { UnitCost = -1m });
        result.ShouldHaveValidationErrorFor(x => x.UnitCost);
    }

    [Fact]
    public void Reference_TropLongue_Echoue()
    {
        var result = _sut.TestValidate(Valid() with { ReferenceNumber = new string('x', 81) });
        result.ShouldHaveValidationErrorFor(x => x.ReferenceNumber);
    }
}
