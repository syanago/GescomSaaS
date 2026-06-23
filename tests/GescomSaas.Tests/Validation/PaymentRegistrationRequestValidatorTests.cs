using FluentValidation.TestHelper;
using GescomSaas.Application.Models;
using GescomSaas.Application.Validation;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Tests.Validation;

public class PaymentRegistrationRequestValidatorTests
{
    private readonly PaymentRegistrationRequestValidator _sut = new();

    private static PaymentRegistrationRequest Valid() => new(
        DocumentId: Guid.NewGuid(),
        PartnerId: Guid.NewGuid(),
        Direction: PaymentDirection.Incoming,
        PaymentDate: DateOnly.FromDateTime(DateTime.UtcNow),
        Amount: 100m,
        Type: PaymentType.Standard,
        AllocationMode: PaymentAllocationMode.Manual,
        Method: PaymentMethod.BankTransfer,
        ReferenceNumber: "REF-001",
        Notes: null);

    [Fact]
    public void RequetValide_PasseSansErreur()
    {
        var result = _sut.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void PartnerId_Vide_Echoue()
    {
        var result = _sut.TestValidate(Valid() with { PartnerId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.PartnerId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Montant_NulOuNegatif_Echoue(decimal amount)
    {
        var result = _sut.TestValidate(Valid() with { Amount = amount });
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void DatePaiement_DansLeFutur_Echoue()
    {
        var futur = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));
        var result = _sut.TestValidate(Valid() with { PaymentDate = futur });
        result.ShouldHaveValidationErrorFor(x => x.PaymentDate);
    }

    [Fact]
    public void DatePaiement_AvantAn2000_Echoue()
    {
        var ancien = new DateOnly(1999, 12, 31);
        var result = _sut.TestValidate(Valid() with { PaymentDate = ancien });
        result.ShouldHaveValidationErrorFor(x => x.PaymentDate);
    }

    [Fact]
    public void Notes_TropLongues_Echoue()
    {
        var result = _sut.TestValidate(Valid() with { Notes = new string('x', 501) });
        result.ShouldHaveValidationErrorFor(x => x.Notes);
    }
}
