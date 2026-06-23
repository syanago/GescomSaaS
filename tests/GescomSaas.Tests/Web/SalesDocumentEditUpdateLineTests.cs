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
/// Tests du handler OnPostUpdateLineAsync sur SalesDocuments/Edit.
/// Couvre l'ensemble des branches de validation/securite ajoutees en Phase 2
/// (edition inline des lignes via AJAX).
/// </summary>
public class SalesDocumentEditUpdateLineTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _db = null!;
    private EditModel _sut = null!;
    private Guid _tenantId;
    private Guid _partnerId;
    private Guid _docId;
    private Guid _lineId;

    public async Task InitializeAsync()
    {
        // SQLite in-memory : meme moteur relationnel qu'en prod (supporte ExecuteUpdate).
        // La connection doit rester ouverte pour preserver la base le temps du test.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new ApplicationDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        _tenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            CompanyName = "TestCo",
            Slug = "testco",
        });

        _partnerId = Guid.NewGuid();
        _db.BusinessPartners.Add(new BusinessPartner
        {
            Id = _partnerId,
            TenantId = _tenantId,
            Code = "C001",
            Name = "Client Alpha",
            PartnerType = BusinessPartnerType.Customer,
        });

        _docId = Guid.NewGuid();
        _lineId = Guid.NewGuid();

        _db.CommercialDocuments.Add(new CommercialDocument
        {
            Id = _docId,
            TenantId = _tenantId,
            DocumentType = CommercialDocumentType.SalesInvoice,
            Status = CommercialDocumentStatus.Draft,
            Number = "F-001",
            CurrencyCode = "EUR",
            PartnerId = _partnerId,
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TotalExcludingTax = 100m,
            TotalTax = 20m,
            TotalIncludingTax = 120m,
        });

        _db.CommercialDocumentLines.Add(new CommercialDocumentLine
        {
            Id = _lineId,
            CommercialDocumentId = _docId,
            Description = "Ligne test",
            Quantity = 2m,
            UnitPriceExcludingTax = 50m,
            DiscountRate = 0m,
            TaxRate = 20m,
            LineTotalExcludingTax = 100m,
            LineTaxAmount = 20m,
            LineTotalIncludingTax = 120m,
        });

        await _db.SaveChangesAsync();

        _sut = BuildSut();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        _connection.Dispose();
        return Task.CompletedTask;
    }

    private EditModel BuildSut()
    {
        var tenantAccessor = new Mock<ICurrentTenantAccessor>();
        tenantAccessor.Setup(x => x.GetTenantId()).Returns(_tenantId);

        var permService = new Mock<IUserPermissionService>();
        var workflow = new Mock<ICommercialDocumentWorkflowService>();
        var numbering = new Mock<INumberingService>();

        var sut = new EditModel(_db, tenantAccessor.Object, permService.Object, workflow.Object, numbering.Object)
        {
            Id = _docId,
        };

        // Cable un PageContext minimal pour que HttpContext.RequestAborted soit accessible
        var actionDescriptor = new CompiledPageActionDescriptor();
        var pageContext = new PageContext(new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            actionDescriptor));
        sut.PageContext = pageContext;

        return sut;
    }

    // -----------------------------------------------------------------
    // Cas valides : maj appliquee, totaux recalcules, JSON renvoye
    // -----------------------------------------------------------------

    [Fact]
    public async Task UpdateLine_AvecDonneesValides_RecalculeLigneEtRenvoieJson()
    {
        // Act : qty 5 x prix 80 - 10% = 360 HT, +20% TVA = 432 TTC
        var result = await _sut.OnPostUpdateLineAsync(_lineId, quantity: 5m, unitPrice: 80m, discountRate: 10m, taxRate: 20m);

        // Assert
        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().BeNull(); // 200 par defaut (pas explicite)

        // Verifie en base que la ligne a bien ete mise a jour avec les totaux recalcules
        var lineInDb = await _db.CommercialDocumentLines.AsNoTracking().FirstAsync(x => x.Id == _lineId);
        lineInDb.Quantity.Should().Be(5m);
        lineInDb.UnitPriceExcludingTax.Should().Be(80m);
        lineInDb.DiscountRate.Should().Be(10m);
        lineInDb.TaxRate.Should().Be(20m);
        lineInDb.LineTotalExcludingTax.Should().Be(360m);
        lineInDb.LineTaxAmount.Should().Be(72m);
        lineInDb.LineTotalIncludingTax.Should().Be(432m);

        // Le payload JSON expose bien les valeurs recalculees
        var payload = json.Value!;
        var ok = (bool)payload.GetType().GetProperty("ok")!.GetValue(payload)!;
        ok.Should().BeTrue();

        var line = payload.GetType().GetProperty("line")!.GetValue(payload)!;
        var subtotal = (decimal)line.GetType().GetProperty("subtotal")!.GetValue(line)!;
        var tax = (decimal)line.GetType().GetProperty("tax")!.GetValue(line)!;
        var total = (decimal)line.GetType().GetProperty("total")!.GetValue(line)!;
        subtotal.Should().Be(360m);
        tax.Should().Be(72m);
        total.Should().Be(432m);
    }

    [Fact]
    public async Task UpdateLine_StatusOpen_AccepteEgalement()
    {
        // Arrange
        var doc = await _db.CommercialDocuments.FindAsync(_docId);
        doc!.Status = CommercialDocumentStatus.Open;
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.OnPostUpdateLineAsync(_lineId, quantity: 3m, unitPrice: 100m, discountRate: 0m, taxRate: 20m);

        // Assert
        var json = result.Should().BeOfType<JsonResult>().Subject;
        var ok = (bool)json.Value!.GetType().GetProperty("ok")!.GetValue(json.Value)!;
        ok.Should().BeTrue();
    }

    // -----------------------------------------------------------------
    // Validation cote serveur : 400 sur valeurs hors-bornes
    // -----------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-0.5)]
    public async Task UpdateLine_QuantityInferieureOuEgaleAZero_Renvoie400(double quantity)
    {
        var result = await _sut.OnPostUpdateLineAsync(_lineId, (decimal)quantity, 50m, 0m, 20m);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
        AssertNotOk(json);
    }

    [Fact]
    public async Task UpdateLine_UnitPriceNegatif_Renvoie400()
    {
        var result = await _sut.OnPostUpdateLineAsync(_lineId, 1m, -1m, 0m, 20m);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
        AssertNotOk(json);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(150)]
    public async Task UpdateLine_DiscountRateHorsBornes_Renvoie400(double discount)
    {
        var result = await _sut.OnPostUpdateLineAsync(_lineId, 1m, 50m, (decimal)discount, 20m);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
        AssertNotOk(json);
    }

    [Theory]
    [InlineData(-0.5)]
    [InlineData(105)]
    public async Task UpdateLine_TaxRateHorsBornes_Renvoie400(double taxRate)
    {
        var result = await _sut.OnPostUpdateLineAsync(_lineId, 1m, 50m, 0m, (decimal)taxRate);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(400);
        AssertNotOk(json);
    }

    // -----------------------------------------------------------------
    // Securite : 404 si doc/ligne inexistants, 409 si statut bloque
    // -----------------------------------------------------------------

    [Fact]
    public async Task UpdateLine_DocumentInexistant_Renvoie404()
    {
        _sut.Id = Guid.NewGuid(); // route id pointe sur un doc inconnu

        var result = await _sut.OnPostUpdateLineAsync(_lineId, 1m, 50m, 0m, 20m);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(404);
        AssertNotOk(json);
    }

    [Fact]
    public async Task UpdateLine_DocumentTenantDifferent_Renvoie404()
    {
        // Arrange : doc cree pour un autre tenant
        var otherTenantDocId = Guid.NewGuid();
        _db.CommercialDocuments.Add(new CommercialDocument
        {
            Id = otherTenantDocId,
            TenantId = Guid.NewGuid(), // autre tenant !
            DocumentType = CommercialDocumentType.SalesInvoice,
            Status = CommercialDocumentStatus.Draft,
            Number = "F-XXX",
            CurrencyCode = "EUR",
            PartnerId = _partnerId,
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await _db.SaveChangesAsync();
        _sut.Id = otherTenantDocId;

        // Act
        var result = await _sut.OnPostUpdateLineAsync(_lineId, 1m, 50m, 0m, 20m);

        // Assert : doit renvoyer 404 (isolation multi-tenant)
        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(404);
    }

    [Theory]
    [InlineData(CommercialDocumentStatus.PartiallyProcessed)]
    [InlineData(CommercialDocumentStatus.Completed)]
    [InlineData(CommercialDocumentStatus.Cancelled)]
    public async Task UpdateLine_StatusNonModifiable_Renvoie409(CommercialDocumentStatus status)
    {
        // Arrange
        var doc = await _db.CommercialDocuments.FindAsync(_docId);
        doc!.Status = status;
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.OnPostUpdateLineAsync(_lineId, 1m, 50m, 0m, 20m);

        // Assert
        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(409);
        AssertNotOk(json);
    }

    [Fact]
    public async Task UpdateLine_LigneInexistante_Renvoie404()
    {
        var result = await _sut.OnPostUpdateLineAsync(Guid.NewGuid(), 1m, 50m, 0m, 20m);

        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(404);
        AssertNotOk(json);
    }

    [Fact]
    public async Task UpdateLine_LigneAppartenantAUnAutreDocument_Renvoie404()
    {
        // Arrange : seconde ligne sur un autre document
        var otherDocId = Guid.NewGuid();
        var otherLineId = Guid.NewGuid();
        _db.CommercialDocuments.Add(new CommercialDocument
        {
            Id = otherDocId,
            TenantId = _tenantId,
            DocumentType = CommercialDocumentType.SalesInvoice,
            Status = CommercialDocumentStatus.Draft,
            Number = "F-002",
            CurrencyCode = "EUR",
            PartnerId = _partnerId,
            DocumentDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        _db.CommercialDocumentLines.Add(new CommercialDocumentLine
        {
            Id = otherLineId,
            CommercialDocumentId = otherDocId,
            Description = "Autre ligne",
            Quantity = 1m,
            UnitPriceExcludingTax = 10m,
            TaxRate = 0m,
        });
        await _db.SaveChangesAsync();

        // Act : on tente de modifier otherLineId via la route _docId
        var result = await _sut.OnPostUpdateLineAsync(otherLineId, 1m, 50m, 0m, 20m);

        // Assert
        var json = result.Should().BeOfType<JsonResult>().Subject;
        json.StatusCode.Should().Be(404);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static void AssertNotOk(JsonResult json)
    {
        var payload = json.Value!;
        var ok = (bool)payload.GetType().GetProperty("ok")!.GetValue(payload)!;
        ok.Should().BeFalse();
        var error = (string)payload.GetType().GetProperty("error")!.GetValue(payload)!;
        error.Should().NotBeNullOrWhiteSpace();
    }
}
