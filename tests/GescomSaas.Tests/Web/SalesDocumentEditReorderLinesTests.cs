using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages.SalesDocuments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Tests.Web;

/// <summary>
/// Tests du handler OnPostReorderLinesAsync sur SalesDocuments/Edit.
/// Couvre la persistance du SortOrder + toutes les branches de validation
/// (liste vide, doublons, IDs etrangers, liste incomplete, status non modifiable, etc.).
/// </summary>
public class SalesDocumentEditReorderLinesTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _db = null!;
    private EditModel _sut = null!;
    private Guid _tenantId;
    private Guid _docId;
    private Guid _line1Id;
    private Guid _line2Id;
    private Guid _line3Id;

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
            Code = "C001",
            Name = "Client",
            PartnerType = BusinessPartnerType.Customer,
        });

        _docId = Guid.NewGuid();
        _line1Id = Guid.NewGuid();
        _line2Id = Guid.NewGuid();
        _line3Id = Guid.NewGuid();

        _db.CommercialDocuments.Add(new CommercialDocument
        {
            Id = _docId,
            TenantId = _tenantId,
            DocumentType = CommercialDocumentType.SalesInvoice,
            Status = CommercialDocumentStatus.Draft,
            Number = "F-001",
            CurrencyCode = "EUR",
            PartnerId = partnerId,
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        // 3 lignes ordonnees 0, 1, 2
        _db.CommercialDocumentLines.AddRange(
            new CommercialDocumentLine { Id = _line1Id, CommercialDocumentId = _docId, Description = "L1", Quantity = 1m, UnitPriceExcludingTax = 10m, TaxRate = 0m, SortOrder = 0 },
            new CommercialDocumentLine { Id = _line2Id, CommercialDocumentId = _docId, Description = "L2", Quantity = 2m, UnitPriceExcludingTax = 20m, TaxRate = 0m, SortOrder = 1 },
            new CommercialDocumentLine { Id = _line3Id, CommercialDocumentId = _docId, Description = "L3", Quantity = 3m, UnitPriceExcludingTax = 30m, TaxRate = 0m, SortOrder = 2 }
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

    // -----------------------------------------------------------------
    // Cas valides
    // -----------------------------------------------------------------

    [Fact]
    public async Task ReorderLines_AvecOrdreInverse_PersisteSortOrder0Pour_DerniereLigne()
    {
        // Act : ordre [L3, L2, L1] -> SortOrder 0=L3, 1=L2, 2=L1
        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line3Id, _line2Id, _line1Id });

        // Assert
        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().BeNull();
        var ok = (bool)json.Value!.GetType().GetProperty("ok")!.GetValue(json.Value)!;
        ok.Should().BeTrue();

        var lines = await _db.CommercialDocumentLines
            .AsNoTracking()
            .Where(x => x.CommercialDocumentId == _docId)
            .ToListAsync();

        lines.Single(x => x.Id == _line3Id).SortOrder.Should().Be(0);
        lines.Single(x => x.Id == _line2Id).SortOrder.Should().Be(1);
        lines.Single(x => x.Id == _line1Id).SortOrder.Should().Be(2);
    }

    [Fact]
    public async Task ReorderLines_AvecOrdreIdentique_PersisteSansChangement()
    {
        // Act : on respecte l'ordre actuel
        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line1Id, _line2Id, _line3Id });

        // Assert : succes, SortOrder inchange
        var json = result.Should().BeOfType<JsonResult>().Subject;
        var ok = (bool)json.Value!.GetType().GetProperty("ok")!.GetValue(json.Value)!;
        ok.Should().BeTrue();

        var lines = await _db.CommercialDocumentLines
            .AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Where(x => x.CommercialDocumentId == _docId)
            .Select(x => x.Id)
            .ToListAsync();

        lines.Should().Equal(_line1Id, _line2Id, _line3Id);
    }

    [Fact]
    public async Task ReorderLines_StatusOpen_AccepteEgalement()
    {
        var doc = await _db.CommercialDocuments.FindAsync(_docId);
        doc!.Status = CommercialDocumentStatus.Open;
        await _db.SaveChangesAsync();

        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line2Id, _line1Id, _line3Id });

        var json = result.Should().BeOfType<JsonResult>().Subject;
        var ok = (bool)json.Value!.GetType().GetProperty("ok")!.GetValue(json.Value)!;
        ok.Should().BeTrue();
    }

    // -----------------------------------------------------------------
    // Validation
    // -----------------------------------------------------------------

    [Fact]
    public async Task ReorderLines_ListeNulle_Renvoie400()
    {
        var result = await _sut.OnPostReorderLinesAsync(null!);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
        AssertNotOk(json);
    }

    [Fact]
    public async Task ReorderLines_ListeVide_Renvoie400()
    {
        var result = await _sut.OnPostReorderLinesAsync(new List<Guid>());

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
        AssertNotOk(json);
    }

    [Fact]
    public async Task ReorderLines_AvecDoublons_Renvoie400()
    {
        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line1Id, _line2Id, _line1Id });

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
        AssertNotOk(json);
    }

    [Fact]
    public async Task ReorderLines_AvecIdEtrangerAuDocument_Renvoie400()
    {
        // L'ID etranger remplace L3 -> meme cardinalite mais l'ID n'existe pas
        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line1Id, _line2Id, Guid.NewGuid() });

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
        AssertNotOk(json);
    }

    [Fact]
    public async Task ReorderLines_ListeIncomplete_Renvoie400()
    {
        // 2 IDs alors qu'il y a 3 lignes en base
        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line1Id, _line2Id });

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
        AssertNotOk(json);
    }

    // -----------------------------------------------------------------
    // Securite
    // -----------------------------------------------------------------

    [Fact]
    public async Task ReorderLines_DocumentInexistant_Renvoie404()
    {
        _sut.Id = Guid.NewGuid();

        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line1Id, _line2Id, _line3Id });

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(404);
    }

    [Theory]
    [InlineData(CommercialDocumentStatus.PartiallyProcessed)]
    [InlineData(CommercialDocumentStatus.Completed)]
    [InlineData(CommercialDocumentStatus.Cancelled)]
    public async Task ReorderLines_StatusNonModifiable_Renvoie409(CommercialDocumentStatus status)
    {
        var doc = await _db.CommercialDocuments.FindAsync(_docId);
        doc!.Status = status;
        await _db.SaveChangesAsync();

        var result = await _sut.OnPostReorderLinesAsync(new List<Guid> { _line1Id, _line2Id, _line3Id });

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(409);
        AssertNotOk(json);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static void AssertNotOk(JsonResult json)
    {
        var payload = json.Value!;
        var ok = (bool)payload.GetType().GetProperty("ok")!.GetValue(payload)!;
        ok.Should().BeFalse();
    }
}
