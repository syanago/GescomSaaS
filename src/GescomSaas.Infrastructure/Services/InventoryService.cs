using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class InventoryService(ApplicationDbContext dbContext) : IInventoryService
{
    public async Task<InventoryDashboardSnapshot> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var productStocks = await dbContext.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TrackStock)
            .Select(x => new
            {
                x.Id,
                x.Sku,
                x.Label,
                x.UnitOfMeasure,
                x.PurchasePrice,
                Quantities = x.StockMovements.Sum(m => (decimal?)m.Quantity) ?? 0m,
                Value = x.StockMovements.Sum(m => (decimal?)(m.Quantity * m.UnitCost)) ?? 0m
            })
            .OrderBy(x => x.Sku)
            .ToListAsync(cancellationToken);

        var mappedProducts = productStocks
            .Select(x =>
            {
                var averageCost = x.Quantities != 0m
                    ? decimal.Round(x.Value / x.Quantities, 2)
                    : x.PurchasePrice;

                return new InventoryStockItem(
                    x.Id,
                    x.Sku,
                    x.Label,
                    x.UnitOfMeasure,
                    x.Quantities,
                    averageCost,
                    decimal.Round(x.Value, 2));
            })
            .ToList();

        var warehouseStocks = await dbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Label,
                x.IsDefault,
                TotalQuantity = dbContext.StockMovements.Where(m => m.TenantId == tenantId && m.WarehouseId == x.Id).Sum(m => (decimal?)m.Quantity) ?? 0m,
                TotalValue = dbContext.StockMovements.Where(m => m.TenantId == tenantId && m.WarehouseId == x.Id).Sum(m => (decimal?)(m.Quantity * m.UnitCost)) ?? 0m
            })
            .OrderByDescending(x => x.TotalValue)
            .ThenBy(x => x.Code)
            .Select(x => new WarehouseInventoryItem(
                x.Id,
                x.Code,
                x.Label,
                x.TotalQuantity,
                x.TotalValue))
            .ToListAsync(cancellationToken);

        return new InventoryDashboardSnapshot(
            mappedProducts.Count,
            mappedProducts.Sum(x => x.OnHandQuantity),
            mappedProducts.Sum(x => x.StockValue),
            mappedProducts,
            warehouseStocks);
    }

    public async Task<IReadOnlyList<InventoryMovementItem>> GetMovementsAsync(Guid tenantId, Guid? productId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.StockMovements
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.Warehouse)
            .Where(x => x.TenantId == tenantId);

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        return await query
            .OrderByDescending(x => x.MovementDate)
            .ThenByDescending(x => x.CreatedOnUtc)
            .Select(x => new InventoryMovementItem(
                x.Id,
                x.MovementDate,
                x.Product != null ? x.Product.Sku : "-",
                x.Product != null ? x.Product.Label : "Article",
                x.Warehouse != null ? x.Warehouse.Code : "-",
                x.Warehouse != null ? x.Warehouse.Label : "Depot",
                x.MovementType,
                x.Quantity,
                x.UnitCost,
                x.Quantity * x.UnitCost,
                x.ReferenceNumber))
            .ToListAsync(cancellationToken);
    }

    public async Task RegisterAdjustmentAsync(Guid tenantId, StockAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0m)
        {
            throw new InvalidOperationException("La quantite doit etre strictement positive.");
        }

        if (request.MovementType is not StockMovementType.AdjustmentIn and not StockMovementType.AdjustmentOut)
        {
            throw new InvalidOperationException("Le type de mouvement doit etre un ajustement d'inventaire.");
        }

        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ProductId && x.TenantId == tenantId, cancellationToken);

        if (product is null || !product.TrackStock)
        {
            throw new InvalidOperationException("Article stockable introuvable.");
        }

        var warehouseExists = await dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.WarehouseId && x.TenantId == tenantId, cancellationToken);

        if (!warehouseExists)
        {
            throw new InvalidOperationException("Depot introuvable.");
        }

        var signedQuantity = request.MovementType == StockMovementType.AdjustmentOut
            ? -request.Quantity
            : request.Quantity;

        dbContext.StockMovements.Add(new StockMovement
        {
            TenantId = tenantId,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            MovementDate = request.MovementDate,
            MovementType = request.MovementType,
            Quantity = signedQuantity,
            UnitCost = request.UnitCost > 0m ? request.UnitCost : product.PurchasePrice,
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : request.ReferenceNumber.Trim()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
