using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages.PurchaseDocuments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Tests.Web;

/// <summary>
/// Smoke tests sur le handler OnPostUpdateLineAsync de PurchaseDocuments/Edit.
/// La logique est symetrique a celle de SalesDocuments — couverture exhaustive
/// faite cote Sales, on valide ici que le handler fournisseur fonctionne aussi
/// (cas valide + branche securitaire critique).
/// </summary>
public class PurchaseDocumentEditUpdateLineTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _db = null!;
    private EditModel _sut = null!;
    private Guid _tenantId;
    private Guid _docId;
    private Guid _lineId;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new ApplicationDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _tenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant { Id = _tenantId, CompanyName = "TestCo", Slug = "testco" });

        var partnerId = Guid.NewGuid();
        _db.BusinessPartners.Add(new BusinessPartner
        {
            Id = partnerId,
            TenantId = _tenantId,
            Code = "S001",
            Name = "Fournisseur Beta",
            PartnerType = BusinessPartnerType.Supplier,
        });

        _docId = Guid.NewGuid();
        _lineId = Guid.NewGuid();

        _db.CommercialDocuments.Add(new CommercialDocument
        {
            Id = _docId,
            TenantId = _tenantId,
            DocumentType = CommercialDocumentType.PurchaseInvoice,
            Status = CommercialDocumentStatus.Draft,
            Number = "FA-001",
            CurrencyCode = "EUR",
            PartnerId = partnerId,
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TotalExcludingTax = 100m,
            TotalTax = 20m,
            TotalIncludingTax = 120m,
        });

        _db.CommercialDocumentLines.Add(new CommercialDocumentLine
        {
            Id = _lineId,
            CommercialDocumentId = _docId,
            Description = "Ligne achat",
            Quantity = 2m,
            UnitPriceExcludingTax = 50m,
            DiscountRate = 0m,
            TaxRate = 20m,
            LineTotalExcludingTax = 100m,
            LineTaxAmount = 20m,
            LineTotalIncludingTax = 120m,
        });

        await _db.SaveChangesAsync();

        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.Setup(x => x.GetTenantId()).Returns(_tenantId);

        var permService = new Mock<IUserPermissionService>();
        var workflow = new Mock<ICommercialDocumentWorkflowService>();
        var numbering = new Mock<INumberingService>();

        _sut = new EditModel(_db, tenantAccessor.Object, permService.Object, workflow.Object, numbering.Object)
        {
            Id = _docId,
        };

        var actionDescriptor = new CompiledPageActionDescriptor();
        _sut.PageContext = new PageContext(new ActionContext(
            new DefaultHttpContext(), new RouteData(), actionDescriptor));
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task UpdateLine_AvecDonneesValides_RecalculeEtRenvoieJsonOK()
    {
        // Act : qty 4 x prix 25 - 0% = 100 HT, +10% TVA = 110 TTC
        var result = await _sut.OnPostUpdateLineAsync(_lineId, 4m, 25m, 0m, 10m);

        // Assert
        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().BeNull();

        var lineInDb = await _db.CommercialDocumentLines.AsNoTracking().FirstAsync(x => x.Id == _lineId);
        lineInDb.LineTotalExcludingTax.Should().Be(100m);
        lineInDb.LineTaxAmount.Should().Be(10m);
        lineInDb.LineTotalIncludingTax.Should().Be(110m);

        var ok = (bool)json.Value!.GetType().GetProperty("ok")!.GetValue(json.Value)!;
        ok.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateLine_QuantityZero_Renvoie400()
    {
        var result = await _sut.OnPostUpdateLineAsync(_lineId, 0m, 25m, 0m, 10m);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateLine_StatusCancelled_Renvoie409()
    {
        var doc = await _db.CommercialDocuments.FindAsync(_docId);
        doc!.Status = CommercialDocumentStatus.Cancelled;
        await _db.SaveChangesAsync();

        var result = await _sut.OnPostUpdateLineAsync(_lineId, 1m, 25m, 0m, 10m);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task UpdateLine_DocumentInexistant_Renvoie404()
    {
        _sut.Id = Guid.NewGuid();
        var result = await _sut.OnPostUpdateLineAsync(_lineId, 1m, 25m, 0m, 10m);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(404);
    }
}
