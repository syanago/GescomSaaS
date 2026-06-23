using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Tests.Services;

/// <summary>
/// Tests sur SettlementService.ApplyAvailableDepositsAsync.
/// Couvre INVOICE_ALREADY_SETTLED et NotFoundException sur facture absente.
/// </summary>
public class SettlementServiceTests : IAsyncLifetime
{
    private ApplicationDbContext _db = null!;
    private SettlementService _sut = null!;
    private Guid _tenantId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"settlement-tests-{Guid.NewGuid()}")
            .Options;

        _db = new ApplicationDbContext(options);
        _sut = new SettlementService(_db);

        _tenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            CompanyName = "Test Co",
            Slug = "test-co",
        });
        await _db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<CommercialDocument> SeedSalesInvoiceAsync(decimal totalIncTax, decimal alreadyPaid)
    {
        var invoice = new CommercialDocument
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DocumentType = CommercialDocumentType.SalesInvoice,
            Number = "INV-001",
            TotalIncludingTax = totalIncTax,
        };
        _db.CommercialDocuments.Add(invoice);

        if (alreadyPaid > 0m)
        {
            // Le service recalcule BalanceAmount via ApplyDocumentSnapshot a partir
            // des PaymentAllocations chargees. Pour que la facture apparaisse comme
            // reglee, il faut donc des allocations reelles, pas juste PaidAmount seede.
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                TenantId = _tenantId,
                ReferenceNumber = "PAY-001",
                Amount = alreadyPaid,
                AllocatedAmount = alreadyPaid,
                AvailableAmount = 0m,
                Direction = PaymentDirection.Incoming,
                PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            };
            _db.Payments.Add(payment);

            invoice.PaymentAllocations.Add(new PaymentAllocation
            {
                PaymentId = payment.Id,
                CommercialDocumentId = invoice.Id,
                AllocatedAmount = alreadyPaid,
            });
        }

        await _db.SaveChangesAsync();
        return invoice;
    }

    [Fact]
    public async Task ApplyAvailableDeposits_FactureDejaReglee_LeveInvoiceAlreadySettled()
    {
        // Arrange : facture totalement payee (PaidAmount == TotalIncludingTax)
        var invoice = await SeedSalesInvoiceAsync(totalIncTax: 100m, alreadyPaid: 100m);

        // Act
        var act = async () => await _sut.ApplyAvailableDepositsAsync(_tenantId, invoice.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("INVOICE_ALREADY_SETTLED");
        ex.Which.HttpStatusCode.Should().Be(422);
    }

    [Fact]
    public async Task ApplyAvailableDeposits_FactureSurpayee_LeveAussiInvoiceAlreadySettled()
    {
        // Cas defensif : si PaidAmount > TotalIncludingTax (surpayement), on
        // doit aussi rejeter car BalanceAmount <= 0.
        var invoice = await SeedSalesInvoiceAsync(totalIncTax: 100m, alreadyPaid: 120m);

        var act = async () => await _sut.ApplyAvailableDepositsAsync(_tenantId, invoice.Id);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("INVOICE_ALREADY_SETTLED");
    }

    [Fact]
    public async Task ApplyAvailableDeposits_FactureInexistante_LeveNotFound()
    {
        var act = async () => await _sut.ApplyAvailableDepositsAsync(_tenantId, Guid.NewGuid());

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.EntityName.Should().Be(nameof(CommercialDocument));
        ex.Which.HttpStatusCode.Should().Be(404);
    }

    [Fact]
    public async Task ApplyAvailableDeposits_FactureDansAutreTenant_LeveNotFound()
    {
        // Garde-fou cross-tenant : facture du _tenantId, accedee depuis un autre tenant.
        var invoice = await SeedSalesInvoiceAsync(totalIncTax: 100m, alreadyPaid: 0m);
        var autreTenantId = Guid.NewGuid();

        var act = async () => await _sut.ApplyAvailableDepositsAsync(autreTenantId, invoice.Id);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.EntityName.Should().Be(nameof(CommercialDocument));
    }

    [Fact]
    public async Task ApplyAvailableDeposits_DocumentNonFacture_LeveNotFound()
    {
        // SalesQuote n'est pas une SalesInvoice : le service filtre par DocumentType,
        // donc le devis n'est pas trouve et on leve NotFound.
        var quote = new CommercialDocument
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            DocumentType = CommercialDocumentType.SalesQuote,
            Number = "Q-001",
        };
        _db.CommercialDocuments.Add(quote);
        await _db.SaveChangesAsync();

        var act = async () => await _sut.ApplyAvailableDepositsAsync(_tenantId, quote.Id);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
