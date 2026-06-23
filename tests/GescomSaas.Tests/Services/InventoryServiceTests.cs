using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Tests.Services;

/// <summary>
/// Tests d'integration sur InventoryService.RegisterAdjustmentAsync.
/// Couvre les codes critiques STOCK_INSUFFICIENT, NotFoundException et
/// les validations metier (quantite, type de mouvement).
/// </summary>
public class InventoryServiceTests : IAsyncLifetime
{
    private ApplicationDbContext _db = null!;
    private InventoryService _sut = null!;

    private Guid _tenantId;
    private Guid _productId;
    private Guid _warehouseId;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"inv-tests-{Guid.NewGuid()}")
            .Options;

        _db = new ApplicationDbContext(options);
        _sut = new InventoryService(_db);

        _tenantId = Guid.NewGuid();
        _productId = Guid.NewGuid();
        _warehouseId = Guid.NewGuid();

        _db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            CompanyName = "Test Co",
            Slug = "test-co",
            AllowNegativeStock = false,
        });

        _db.Products.Add(new Product
        {
            Id = _productId,
            TenantId = _tenantId,
            Sku = "SKU-001",
            Label = "Test Product",
            TrackStock = true,
            StockIdentityTrackingMode = StockIdentityTrackingMode.None,
            PurchasePrice = 10m,
        });

        _db.Warehouses.Add(new Warehouse
        {
            Id = _warehouseId,
            TenantId = _tenantId,
            Code = "MAIN",
            Label = "Main",
            IsDefault = true,
        });

        await _db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task RegisterAdjustment_QuandStockNegatifInterdit_LeveStockInsufficient()
    {
        // Arrange : aucun stock initial, tentative de sortie -> doit echouer
        var request = new StockAdjustmentRequest(
            ProductId: _productId,
            WarehouseId: _warehouseId,
            MovementDate: DateOnly.FromDateTime(DateTime.UtcNow),
            MovementType: StockMovementType.AdjustmentOut,
            Quantity: 5m,
            UnitCost: 10m,
            ReferenceNumber: null,
            LotNumber: null,
            SerialNumber: null,
            ExpirationDate: null);

        // Act
        var act = async () => await _sut.RegisterAdjustmentAsync(_tenantId, request);

        // Assert : code stable + metadonnees structurees
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("STOCK_INSUFFICIENT");
        ex.Which.HttpStatusCode.Should().Be(422);
        ex.Which.Data["sku"].Should().Be("SKU-001");
        ex.Which.Data["available"].Should().Be(0m);
        ex.Which.Data["requested"].Should().Be(5m);
    }

    [Fact]
    public async Task RegisterAdjustment_QuandTenantAutoriseStockNegatif_PasseMalgreStockInsuffisant()
    {
        // Arrange : on autorise le stock negatif sur ce tenant
        var tenant = await _db.Tenants.FirstAsync(x => x.Id == _tenantId);
        tenant.AllowNegativeStock = true;
        await _db.SaveChangesAsync();

        var request = new StockAdjustmentRequest(
            _productId, _warehouseId, DateOnly.FromDateTime(DateTime.UtcNow),
            StockMovementType.AdjustmentOut, 5m, 10m, null, null, null, null);

        // Act + Assert : aucune exception
        var act = async () => await _sut.RegisterAdjustmentAsync(_tenantId, request);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RegisterAdjustment_AvecStockSuffisant_ReussitEtCreeLeMouvement()
    {
        // Arrange : seed d'un mouvement d'entree pour avoir 10 unites en stock
        _db.StockMovements.Add(new StockMovement
        {
            TenantId = _tenantId,
            ProductId = _productId,
            WarehouseId = _warehouseId,
            MovementDate = DateOnly.FromDateTime(DateTime.UtcNow),
            MovementType = StockMovementType.AdjustmentIn,
            Quantity = 10m,
            UnitCost = 10m,
        });
        await _db.SaveChangesAsync();

        var request = new StockAdjustmentRequest(
            _productId, _warehouseId, DateOnly.FromDateTime(DateTime.UtcNow),
            StockMovementType.AdjustmentOut, 5m, 10m, null, null, null, null);

        // Act
        await _sut.RegisterAdjustmentAsync(_tenantId, request);

        // Assert : mouvement enregistre avec quantite signee negative
        var movements = await _db.StockMovements.AsNoTracking().ToListAsync();
        movements.Should().HaveCount(2);
        movements.Should().Contain(m => m.Quantity == -5m && m.MovementType == StockMovementType.AdjustmentOut);
    }

    [Fact]
    public async Task RegisterAdjustment_QuandQuantiteNulleOuNegative_LeveBusinessRule()
    {
        var request = new StockAdjustmentRequest(
            _productId, _warehouseId, DateOnly.FromDateTime(DateTime.UtcNow),
            StockMovementType.AdjustmentOut, 0m, 10m, null, null, null, null);

        var act = async () => await _sut.RegisterAdjustmentAsync(_tenantId, request);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("STOCK_ADJUSTMENT_QUANTITY_INVALID");
    }

    [Fact]
    public async Task RegisterAdjustment_QuandTypeNonAjustement_LeveBusinessRule()
    {
        var request = new StockAdjustmentRequest(
            _productId, _warehouseId, DateOnly.FromDateTime(DateTime.UtcNow),
            // Receipt n'est pas un ajustement valide pour RegisterAdjustment
            StockMovementType.Receipt, 5m, 10m, null, null, null, null);

        var act = async () => await _sut.RegisterAdjustmentAsync(_tenantId, request);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("STOCK_ADJUSTMENT_TYPE_INVALID");
    }

    [Fact]
    public async Task RegisterAdjustment_QuandProduitInexistant_LeveNotFound()
    {
        var request = new StockAdjustmentRequest(
            ProductId: Guid.NewGuid(),
            WarehouseId: _warehouseId,
            MovementDate: DateOnly.FromDateTime(DateTime.UtcNow),
            MovementType: StockMovementType.AdjustmentIn,
            Quantity: 5m,
            UnitCost: 10m,
            ReferenceNumber: null,
            LotNumber: null,
            SerialNumber: null,
            ExpirationDate: null);

        var act = async () => await _sut.RegisterAdjustmentAsync(_tenantId, request);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        // "StockableProduct" et non "Product" : le service ne peut ajuster
        // que des produits avec TrackStock=true.
        ex.Which.EntityName.Should().Be("StockableProduct");
        ex.Which.HttpStatusCode.Should().Be(404);
    }

    [Fact]
    public async Task RegisterAdjustment_QuandDepotInexistant_LeveNotFound()
    {
        var request = new StockAdjustmentRequest(
            ProductId: _productId,
            WarehouseId: Guid.NewGuid(),
            MovementDate: DateOnly.FromDateTime(DateTime.UtcNow),
            MovementType: StockMovementType.AdjustmentIn,
            Quantity: 5m,
            UnitCost: 10m,
            ReferenceNumber: null,
            LotNumber: null,
            SerialNumber: null,
            ExpirationDate: null);

        var act = async () => await _sut.RegisterAdjustmentAsync(_tenantId, request);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.EntityName.Should().Be("Warehouse");
    }

    [Fact]
    public async Task RegisterAdjustment_ProduitGereParLot_SansLot_LeveBusinessRule()
    {
        var product = await _db.Products.FirstAsync(x => x.Id == _productId);
        product.StockIdentityTrackingMode = StockIdentityTrackingMode.Lot;
        await _db.SaveChangesAsync();

        var request = new StockAdjustmentRequest(
            _productId, _warehouseId, DateOnly.FromDateTime(DateTime.UtcNow),
            StockMovementType.AdjustmentIn, 5m, 10m, null,
            LotNumber: null,
            SerialNumber: null,
            ExpirationDate: null);

        var act = async () => await _sut.RegisterAdjustmentAsync(_tenantId, request);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.ErrorCode.Should().Be("STOCK_LOT_NUMBER_REQUIRED");
        ex.Which.Data["sku"].Should().Be("SKU-001");
    }
}
