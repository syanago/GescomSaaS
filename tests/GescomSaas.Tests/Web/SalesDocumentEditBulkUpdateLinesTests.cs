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
/// Tests sur OnPostBulkUpdateLinesAsync - mise a jour groupee
/// (apply remise / taxe a une selection de lignes).
/// </summary>
public class SalesDocumentEditBulkUpdateLinesTests : IAsyncLifetime
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

        // 3 lignes : qty=10, prix=100, remise=0, taxe=0 (totaux 1000 HT, 0 tax, 1000 TTC chacune)
        _db.CommercialDocumentLines.AddRange(
            new CommercialDocumentLine
            {
                Id = _line1Id,
                CommercialDocumentId = _docId,
                Description = "L1",
                Quantity = 10m,
                UnitPriceExcludingTax = 100m,
                DiscountRate = 0m,
                TaxRate = 0m,
                LineTotalExcludingTax = 1000m,
                LineTaxAmount = 0m,
                LineTotalIncludingTax = 1000m,
                SortOrder = 0,
            },
            new CommercialDocumentLine
            {
                Id = _line2Id,
                CommercialDocumentId = _docId,
                Description = "L2",
                Quantity = 10m,
                UnitPriceExcludingTax = 100m,
                DiscountRate = 0m,
                TaxRate = 0m,
                LineTotalExcludingTax = 1000m,
                LineTaxAmount = 0m,
                LineTotalIncludingTax = 1000m,
                SortOrder = 1,
            },
            new CommercialDocumentLine
            {
                Id = _line3Id,
                CommercialDocumentId = _docId,
                Description = "L3",
                Quantity = 10m,
                UnitPriceExcludingTax = 100m,
                DiscountRate = 0m,
                TaxRate = 0m,
                LineTotalExcludingTax = 1000m,
                LineTaxAmount = 0m,
                LineTotalIncludingTax = 1000m,
                SortOrder = 2,
            }
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
    // Cas valides : application de remise et/ou taxe
    // -----------------------------------------------------------------

    [Fact]
    public async Task BulkUpdate_AppliqueTaxe20PourcentSur2Lignes_RecalculeEtPersisteCorrectement()
    {
        // Act
        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id, _line2Id },
            discountRate: null,
            taxRate: 20m);

        // Assert : les 2 lignes ciblees ont taxe=20%, la 3eme reste a 0%
        var json = result.Should().BeOfType<JsonResult>().Subject;
        var ok = (bool)json.Value!.GetType().GetProperty("ok")!.GetValue(json.Value)!;
        ok.Should().BeTrue();

        var l1 = await _db.CommercialDocumentLines.AsNoTracking().FirstAsync(x => x.Id == _line1Id);
        var l2 = await _db.CommercialDocumentLines.AsNoTracking().FirstAsync(x => x.Id == _line2Id);
        var l3 = await _db.CommercialDocumentLines.AsNoTracking().FirstAsync(x => x.Id == _line3Id);

        l1.TaxRate.Should().Be(20m);
        l1.LineTaxAmount.Should().Be(200m);    // 1000 * 20% = 200
        l1.LineTotalIncludingTax.Should().Be(1200m); // 1000 + 200

        l2.TaxRate.Should().Be(20m);
        l2.LineTotalIncludingTax.Should().Be(1200m);

        l3.TaxRate.Should().Be(0m); // non touchee
        l3.LineTotalIncludingTax.Should().Be(1000m);
    }

    [Fact]
    public async Task BulkUpdate_AppliqueRemise10PourcentSurToutes_RecalculeBienLesTotaux()
    {
        // Act : remise 10% sur les 3 lignes
        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id, _line2Id, _line3Id },
            discountRate: 10m,
            taxRate: null);

        // Assert
        var json = result.Should().BeOfType<JsonResult>().Subject;
        var ok = (bool)json.Value!.GetType().GetProperty("ok")!.GetValue(json.Value)!;
        ok.Should().BeTrue();

        var lines = await _db.CommercialDocumentLines
            .AsNoTracking()
            .Where(x => x.CommercialDocumentId == _docId)
            .ToListAsync();

        // 1000 * (1 - 0.10) = 900
        lines.Should().AllSatisfy(l =>
        {
            l.DiscountRate.Should().Be(10m);
            l.LineTotalExcludingTax.Should().Be(900m);
            l.LineTotalIncludingTax.Should().Be(900m); // taxe restee a 0
        });
    }

    [Fact]
    public async Task BulkUpdate_AppliqueRemiseEtTaxeSimultaneement()
    {
        // Act : remise 10% + taxe 20% sur la 1ere ligne
        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id },
            discountRate: 10m,
            taxRate: 20m);

        // Assert
        var json = result.Should().BeOfType<JsonResult>().Subject;
        var ok = (bool)json.Value!.GetType().GetProperty("ok")!.GetValue(json.Value)!;
        ok.Should().BeTrue();

        var l1 = await _db.CommercialDocumentLines.AsNoTracking().FirstAsync(x => x.Id == _line1Id);
        l1.DiscountRate.Should().Be(10m);
        l1.TaxRate.Should().Be(20m);
        l1.LineTotalExcludingTax.Should().Be(900m);    // 1000 - 10%
        l1.LineTaxAmount.Should().Be(180m);            // 900 * 20%
        l1.LineTotalIncludingTax.Should().Be(1080m);   // 900 + 180
    }

    // -----------------------------------------------------------------
    // Validation : 400 sur erreurs entree
    // -----------------------------------------------------------------

    [Fact]
    public async Task BulkUpdate_ListeVide_Renvoie400()
    {
        var result = await _sut.OnPostBulkUpdateLinesAsync(new List<Guid>(), 10m, null);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task BulkUpdate_AucuneValeurFournie_Renvoie400()
    {
        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id }, discountRate: null, taxRate: null);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task BulkUpdate_DoublonsDansSelection_Renvoie400()
    {
        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id, _line1Id }, 10m, null);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task BulkUpdate_RemiseHorsBornes_Renvoie400(double discount)
    {
        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id }, (decimal)discount, null);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(150)]
    public async Task BulkUpdate_TaxeHorsBornes_Renvoie400(double tax)
    {
        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id }, null, (decimal)tax);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task BulkUpdate_AvecLigneEtrangere_Renvoie400()
    {
        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id, Guid.NewGuid() }, 10m, null);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
    }

    // -----------------------------------------------------------------
    // Securite
    // -----------------------------------------------------------------

    [Fact]
    public async Task BulkUpdate_DocumentInexistant_Renvoie404()
    {
        _sut.Id = Guid.NewGuid();

        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id }, 10m, null);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task BulkUpdate_StatusCancelled_Renvoie409()
    {
        var doc = await _db.CommercialDocuments.FindAsync(_docId);
        doc!.Status = CommercialDocumentStatus.Cancelled;
        await _db.SaveChangesAsync();

        var result = await _sut.OnPostBulkUpdateLinesAsync(
            new List<Guid> { _line1Id }, 10m, null);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(409);
    }
}
