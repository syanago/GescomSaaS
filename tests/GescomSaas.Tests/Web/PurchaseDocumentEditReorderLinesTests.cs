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
/// Smoke tests sur OnPostReorderLinesAsync de PurchaseDocuments/Edit.
/// La couverture exhaustive est cote Sales — ici on valide juste la symetrie
/// (cas valide + 3 branches critiques de securite/validation).
/// </summary>
public class PurchaseDocumentEditReorderLinesTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _db = null!;
    private EditModel _sut = null!;
    private Guid _tenantId;
    private Guid _docId;
    private Guid _line1Id;
    private Guid _line2Id;

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
            Name = "Fournisseur",
            PartnerType = BusinessPartnerType.Supplier,
        });

        _docId = Guid.NewGuid();
        _line1Id = Guid.NewGuid();
        _line2Id = Guid.NewGuid();

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
        });

        _db.CommercialDocumentLines.AddRange(
            new CommercialDocumentLine { Id = _line1Id, CommercialDocumentId = _docId, Description = "L1", Quantity = 1m, UnitPriceExcludingTax = 10m, TaxRate = 0m, SortOrder = 0 },
            new CommercialDocumentLine { Id = _line2Id, CommercialDocumentId = _docId, Description = "L2", Quantity = 2m, UnitPriceExcludingTax = 20m, TaxRate = 0m, SortOrder = 1 }
        );

        await _db.SaveChangesAsync();

        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.Setup(x => x.GetTenantId()).Returns(_tenantId);

        _sut = new EditModel(
            _db,
            tenantAccessor.Object,
            new Mock<IUserPermissionService>().Object,
            new Mock<ICommercialDocumentWorkflowService>().Object,
            new Mock<INumberingService>().Object)
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
    public async Task ReorderLines_AvecOrdreInverse_PersisteSortOrder()
    {
        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line2Id, _line1Id });

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().BeNull();

        var lines = await _db.CommercialDocumentLines
            .AsNoTracking()
            .Where(x => x.CommercialDocumentId == _docId)
            .ToListAsync();

        lines.Single(x => x.Id == _line2Id).SortOrder.Should().Be(0);
        lines.Single(x => x.Id == _line1Id).SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task ReorderLines_ListeVide_Renvoie400()
    {
        var result = await _sut.OnPostReorderLinesAsync(new List<Guid>());

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ReorderLines_StatusCancelled_Renvoie409()
    {
        var doc = await _db.CommercialDocuments.FindAsync(_docId);
        doc!.Status = CommercialDocumentStatus.Cancelled;
        await _db.SaveChangesAsync();

        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line1Id, _line2Id });

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task ReorderLines_DocumentInexistant_Renvoie404()
    {
        _sut.Id = Guid.NewGuid();

        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line1Id, _line2Id });

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(404);
    }
}
