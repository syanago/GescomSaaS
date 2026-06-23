using GescomSaas.Domain.Exceptions;

namespace GescomSaas.Tests.Domain;

/// <summary>
/// Tests de la hierarchie d'exceptions metier - garantit que chaque type
/// expose le bon code d'erreur et le bon statut HTTP attendu par le middleware.
/// </summary>
public class AppExceptionTests
{
    [Fact]
    public void NotFoundException_DoitExposerStatut404EtCleEntite()
    {
        var ex = new NotFoundException("BusinessPartner", 42);

        ex.HttpStatusCode.Should().Be(404);
        ex.ErrorCode.Should().Be("NOT_FOUND");
        ex.EntityName.Should().Be("BusinessPartner");
        ex.Key.Should().Be(42);
        ex.Message.Should().Contain("BusinessPartner");
        ex.Message.Should().Contain("42");
    }

    [Fact]
    public void BusinessRuleException_DoitExposerStatut422()
    {
        var ex = new BusinessRuleException("Transition devis -> facture interdite.");

        ex.HttpStatusCode.Should().Be(422);
        ex.ErrorCode.Should().Be("BUSINESS_RULE_VIOLATION");
    }

    [Fact]
    public void BusinessRuleException_PeutPorterUnCodeMetierPersonnalise()
    {
        var ex = new BusinessRuleException("...", errorCode: "DOC_INVALID_TRANSITION");

        ex.ErrorCode.Should().Be("DOC_INVALID_TRANSITION");
    }

    [Fact]
    public void QuotaExceededException_DoitExposer402EtMetadonneesQuota()
    {
        var ex = new QuotaExceededException("users", limit: 10, current: 11);

        ex.HttpStatusCode.Should().Be(402);
        ex.ErrorCode.Should().Be("QUOTA_EXCEEDED");
        ex.QuotaName.Should().Be("users");
        ex.Limit.Should().Be(10);
        ex.Current.Should().Be(11);
        ex.Message.Should().Contain("11/10");
    }

    [Fact]
    public void TenantAccessDeniedException_DoitExposer403()
    {
        var ex = new TenantAccessDeniedException();

        ex.HttpStatusCode.Should().Be(403);
        ex.ErrorCode.Should().Be("TENANT_ACCESS_DENIED");
    }

    [Fact]
    public void ValidationException_DoitExposer400EtErreursParChamp()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Name"] = new[] { "Le nom est requis." },
            ["Email"] = new[] { "Format invalide.", "Domaine non autorise." },
        };

        var ex = new ValidationException(errors);

        ex.HttpStatusCode.Should().Be(400);
        ex.ErrorCode.Should().Be("VALIDATION_FAILED");
        ex.Errors.Should().ContainKey("Email");
        ex.Errors["Email"].Should().HaveCount(2);
    }

    [Fact]
    public void ToutesLesAppExceptions_HeritenDeAppException()
    {
        AppException[] all =
        {
            new NotFoundException("X", 1),
            new BusinessRuleException("x"),
            new QuotaExceededException("x", 1, 2),
            new TenantAccessDeniedException(),
            new ValidationException(new Dictionary<string, string[]>()),
        };

        all.Should().AllSatisfy(e => e.Should().BeAssignableTo<AppException>());
        all.Select(e => e.HttpStatusCode).Should().OnlyContain(s => s >= 400 && s < 500);
    }
}
