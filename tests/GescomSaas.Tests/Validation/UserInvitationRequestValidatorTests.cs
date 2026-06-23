using FluentValidation.TestHelper;
using GescomSaas.Application.Models;
using GescomSaas.Application.Validation;

namespace GescomSaas.Tests.Validation;

public class UserInvitationRequestValidatorTests
{
    private readonly UserInvitationRequestValidator _sut = new();

    private static UserInvitationRequest Valid() => new(
        Email: "user@demo.local",
        FirstName: "Alice",
        LastName: "Martin",
        Roles: new[] { "TenantOwner" },
        Notes: null);

    [Fact]
    public void RequetValide_PasseSansErreur()
    {
        var result = _sut.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pas-un-email")]
    [InlineData("@manque-le-local")]
    public void Email_InvalideOuVide_Echoue(string email)
    {
        var result = _sut.TestValidate(Valid() with { Email = email });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Email_TropLong_Echoue()
    {
        // 251 + "@x.com" = 257 caracteres, juste au-dessus de la limite 256
        var trop = new string('x', 251) + "@x.com";
        var result = _sut.TestValidate(Valid() with { Email = trop });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Roles_Vide_Echoue()
    {
        var result = _sut.TestValidate(Valid() with { Roles = Array.Empty<string>() });
        result.ShouldHaveValidationErrorFor(x => x.Roles);
    }

    [Fact]
    public void Roles_ContientUneValeurVide_Echoue()
    {
        var result = _sut.TestValidate(Valid() with { Roles = new[] { "TenantOwner", "" } });
        result.ShouldHaveValidationErrorFor(x => x.Roles);
    }

    [Fact]
    public void Prenom_TropLong_Echoue()
    {
        var result = _sut.TestValidate(Valid() with { FirstName = new string('x', 81) });
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }
}
