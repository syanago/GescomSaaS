using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Tests.Services;

/// <summary>
/// Tests sur CommercialDocumentWorkflowService.CreateFromSourceAsync.
/// Cible le code DOC_INVALID_TRANSITION et la coherence des transitions
/// devis -> commande -> livraison -> facture.
/// </summary>
public class WorkflowServiceTests : IAsyncLifetime
{
    private ApplicationDbContext _db = null!;
    private CommercialDocumentWorkflowService _sut = null!;
    private Guid _tenantId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"workflow-tests-{Guid.NewGuid()}")
            .Options;

        _db = new ApplicationDbContext(options);

        // Mocks vides : la validation des transitions s'execute AVANT
        // tout appel aux services dependants. Les transitions interdites
        // doivent donc lever sans toucher aux quotas / settlement / numbering.
        var quotaMock = new Mock<ITenantQuotaEnforcementService>(MockBehavior.Loose);
        var settlementMock = new Mock<ISettlementService>(MockBehavior.Loose);
        var numberingMock = new Mock<INumberingService>(MockBehavior.Loose);

        _sut = new CommercialDocumentWorkflowService(
            _db,
            quotaMock.Object,
            settlementMock.Object,
            numberingMock.Object);

        _tenantId = Guid.NewGuid();
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Guid> SeedDocumentAsync(CommercialDocumentType type)
    {
        var doc = new CommercialDocument
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DocumentType = type,
            Number = $"DOC-{type}",
            PartnerId = Guid.Empty,
        };
        _db.CommercialDocuments.Add(doc);
        await _db.SaveChangesAsync();
        return doc.Id;
    }

    [Theory]
    // Transitions interdites au sens metier : on saute des etapes
    [InlineData(CommercialDocumentType.SalesQuote, CommercialDocumentType.SalesInvoice)]
    [InlineData(CommercialDocumentType.SalesQuote, CommercialDocumentType.SalesCreditNote)]
    [InlineData(CommercialDocumentType.DeliveryNote, CommercialDocumentType.SalesQuote)]
    // Aller-retour interdit : facture -> commande
    [InlineData(CommercialDocumentType.SalesInvoice, CommercialDocumentType.SalesOrder)]
    // Croisement vente/achat
    [InlineData(CommercialDocumentType.SalesQuote, CommercialDocumentType.PurchaseOrder)]
    public async Task CreateFromSource_TransitionInterdite_LeveDocInvalidTransition(
        CommercialDocumentType source, CommercialDocumentType target)
    {
        var sourceId = await SeedDocumentAsync(source);

        var act = async () => await _sut.CreateFromSourceAsync(_tenantId, sourceId, target);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("DOC_INVALID_TRANSITION");
        ex.Which.HttpStatusCode.Should().Be(422);
        ex.Which.Message.Should().Contain(source.ToString());
        ex.Which.Message.Should().Contain(target.ToString());
    }

    [Fact]
    public async Task CreateFromSource_DocumentSourceInexistant_LeveNotFound()
    {
        var act = async () => await _sut.CreateFromSourceAsync(
            _tenantId,
            Guid.NewGuid(),
            CommercialDocumentType.SalesOrder);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.EntityName.Should().Be(nameof(CommercialDocument));
        ex.Which.HttpStatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CreateFromSource_DocumentSourceDansAutreTenant_LeveNotFound()
    {
        // Document seede sur _tenantId, mais on demande la transition pour un autre tenant
        var sourceId = await SeedDocumentAsync(CommercialDocumentType.SalesQuote);
        var autreTenantId = Guid.NewGuid();

        var act = async () => await _sut.CreateFromSourceAsync(
            autreTenantId,
            sourceId,
            CommercialDocumentType.SalesOrder);

        // Pas de fuite cross-tenant : on retourne 404 (pas 403)
        // pour empecher l'enumeration des IDs entre tenants.
        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.EntityName.Should().Be(nameof(CommercialDocument));
    }
}
