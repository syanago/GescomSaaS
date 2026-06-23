using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Domain.Exceptions;
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
            .ToListAsync(cancellationToken);

        var mappedWarehouses = warehouseStocks
            .OrderByDescending(x => x.TotalValue)
            .ThenBy(x => x.Code)
            .Select(x => new WarehouseInventoryItem(
                x.Id,
                x.Code,
                x.Label,
                x.TotalQuantity,
                x.TotalValue))
            .ToList();

        return new InventoryDashboardSnapshot(
            mappedProducts.Count,
            mappedProducts.Sum(x => x.OnHandQuantity),
            mappedProducts.Sum(x => x.StockValue),
            mappedProducts,
            mappedWarehouses);
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
                x.ReferenceNumber,
                x.LotNumber,
                x.SerialNumber,
                x.ExpirationDate))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryLotItem>> GetLotStocksAsync(Guid tenantId, Guid? productId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.LotNumber != null);

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        var groupedLots = await query
            .GroupBy(x => new
            {
                x.ProductId,
                ProductCode = x.Product!.Sku,
                ProductLabel = x.Product!.Label,
                x.WarehouseId,
                WarehouseCode = x.Warehouse!.Code,
                WarehouseLabel = x.Warehouse!.Label,
                x.LotNumber,
                x.ExpirationDate
            })
            .Select(x => new
            {
                x.Key.ProductId,
                x.Key.ProductCode,
                x.Key.ProductLabel,
                x.Key.WarehouseId,
                x.Key.WarehouseCode,
                x.Key.WarehouseLabel,
                x.Key.LotNumber,
                x.Key.ExpirationDate,
                OnHandQuantity = x.Sum(y => y.Quantity),
                StockValue = x.Sum(y => y.Quantity * y.UnitCost),
                LastMovementDate = x.Max(y => y.MovementDate)
            })
            .OrderBy(x => x.ProductCode)
            .ThenBy(x => x.LotNumber)
            .ThenBy(x => x.WarehouseCode)
            .ToListAsync(cancellationToken);

        return groupedLots
            .Where(x => x.OnHandQuantity > 0m)
            .Select(x => new InventoryLotItem(
                x.ProductId,
                x.ProductCode,
                x.ProductLabel,
                x.WarehouseId,
                x.WarehouseCode,
                x.WarehouseLabel,
                x.LotNumber!,
                x.ExpirationDate,
                x.OnHandQuantity,
                x.OnHandQuantity != 0m ? decimal.Round(x.StockValue / x.OnHandQuantity, 2) : 0m,
                decimal.Round(x.StockValue, 2),
                x.LastMovementDate))
            .ToList();
    }

    public async Task<IReadOnlyList<InventorySerialItem>> GetSerialStocksAsync(Guid tenantId, Guid? productId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.SerialNumber != null);

        if (productId.HasValue)
        {
            query = query.Where(x => x.ProductId == productId.Value);
        }

        if (warehouseId.HasValue)
        {
            query = query.Where(x => x.WarehouseId == warehouseId.Value);
        }

        var groupedSerials = await query
            .GroupBy(x => new
            {
                x.ProductId,
                ProductCode = x.Product!.Sku,
                ProductLabel = x.Product!.Label,
                x.WarehouseId,
                WarehouseCode = x.Warehouse!.Code,
                WarehouseLabel = x.Warehouse!.Label,
                x.SerialNumber
            })
            .Select(x => new
            {
                x.Key.ProductId,
                x.Key.ProductCode,
                x.Key.ProductLabel,
                x.Key.WarehouseId,
                x.Key.WarehouseCode,
                x.Key.WarehouseLabel,
                x.Key.SerialNumber,
                OnHandQuantity = x.Sum(y => y.Quantity),
                StockValue = x.Sum(y => y.Quantity * y.UnitCost),
                LastMovementDate = x.Max(y => y.MovementDate)
            })
            .OrderBy(x => x.ProductCode)
            .ThenBy(x => x.SerialNumber)
            .ThenBy(x => x.WarehouseCode)
            .ToListAsync(cancellationToken);

        return groupedSerials
            .Where(x => x.OnHandQuantity > 0m)
            .Select(x => new InventorySerialItem(
                x.ProductId,
                x.ProductCode,
                x.ProductLabel,
                x.WarehouseId,
                x.WarehouseCode,
                x.WarehouseLabel,
                x.SerialNumber!,
                x.OnHandQuantity,
                x.OnHandQuantity != 0m ? decimal.Round(x.StockValue / x.OnHandQuantity, 2) : 0m,
                decimal.Round(x.StockValue, 2),
                x.LastMovementDate))
            .ToList();
    }

    public async Task PostStockDocumentAsync(Guid tenantId, StockDocument document, CancellationToken cancellationToken = default)
    {
        if (document.Status == StockDocumentStatus.Posted)
        {
            throw new BusinessRuleException(
                "Ce document de stock est deja valide.",
                errorCode: "STOCK_DOC_ALREADY_POSTED");
        }

        if (document.Lines.Count == 0)
        {
            throw new BusinessRuleException(
                "Ajoute au moins une ligne avant de valider ce document.",
                errorCode: "STOCK_DOC_NO_LINES");
        }

        await ValidateStockDocumentWarehousesAsync(tenantId, document, cancellationToken);

        foreach (var line in document.Lines.Where(x => x.ProductId.HasValue))
        {
            if (line.Quantity <= 0m)
            {
                throw new BusinessRuleException(
                    "Chaque ligne de document de stock doit avoir une quantite strictement positive.",
                    errorCode: "STOCK_LINE_QUANTITY_INVALID");
            }

            var productContext = await GetTrackedProductContextAsync(tenantId, line.ProductId!.Value, cancellationToken);
            await EnsureIdentityTrackingAsync(productContext.Product, line.Quantity, line.LotNumber, line.SerialNumber);

            switch (document.DocumentType)
            {
                case StockDocumentType.Entry:
                    dbContext.StockMovements.Add(BuildStockMovement(
                        tenantId,
                        productContext.Product.Id,
                        document.DestinationWarehouseId!.Value,
                        document.DocumentDate,
                        StockMovementType.Receipt,
                        line.Quantity,
                        line.UnitCost > 0m ? line.UnitCost : productContext.Product.PurchasePrice,
                        document.Number,
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate));
                    break;

                case StockDocumentType.Exit:
                    await EnsureNegativeStockPolicyAsync(
                        productContext,
                        document.SourceWarehouseId!.Value,
                        -line.Quantity,
                        line.LotNumber,
                        line.SerialNumber,
                        cancellationToken);

                    var issueCost = await ResolveIssueUnitCostAsync(
                        productContext,
                        document.SourceWarehouseId.Value,
                        line.LotNumber,
                        line.SerialNumber,
                        cancellationToken);

                    dbContext.StockMovements.Add(BuildStockMovement(
                        tenantId,
                        productContext.Product.Id,
                        document.SourceWarehouseId.Value,
                        document.DocumentDate,
                        StockMovementType.Issue,
                        -line.Quantity,
                        issueCost,
                        document.Number,
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate));
                    break;

                case StockDocumentType.Transfer:
                    await EnsureNegativeStockPolicyAsync(
                        productContext,
                        document.SourceWarehouseId!.Value,
                        -line.Quantity,
                        line.LotNumber,
                        line.SerialNumber,
                        cancellationToken);

                    var transferCost = await ResolveIssueUnitCostAsync(
                        productContext,
                        document.SourceWarehouseId.Value,
                        line.LotNumber,
                        line.SerialNumber,
                        cancellationToken);

                    dbContext.StockMovements.Add(BuildStockMovement(
                        tenantId,
                        productContext.Product.Id,
                        document.SourceWarehouseId.Value,
                        document.DocumentDate,
                        StockMovementType.Transfer,
                        -line.Quantity,
                        transferCost,
                        $"{document.Number}-OUT",
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate));

                    dbContext.StockMovements.Add(BuildStockMovement(
                        tenantId,
                        productContext.Product.Id,
                        document.DestinationWarehouseId!.Value,
                        document.DocumentDate,
                        StockMovementType.Transfer,
                        line.Quantity,
                        transferCost,
                        $"{document.Number}-IN",
                        line.LotNumber,
                        line.SerialNumber,
                        line.ExpirationDate));
                    break;
            }
        }

        document.Status = StockDocumentStatus.Posted;
        document.PostedOnUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RegisterAdjustmentAsync(Guid tenantId, StockAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0m)
        {
            throw new BusinessRuleException(
                "La quantite doit etre strictement positive.",
                errorCode: "STOCK_ADJUSTMENT_QUANTITY_INVALID");
        }

        if (request.MovementType is not StockMovementType.AdjustmentIn and not StockMovementType.AdjustmentOut)
        {
            throw new BusinessRuleException(
                "Le type de mouvement doit etre un ajustement d'inventaire.",
                errorCode: "STOCK_ADJUSTMENT_TYPE_INVALID");
        }

        var productContext = await GetTrackedProductContextAsync(tenantId, request.ProductId, cancellationToken);
        if (productContext is null)
        {
            throw new NotFoundException(nameof(Product), request.ProductId);
        }
        await EnsureWarehouseExistsAsync(tenantId, request.WarehouseId, cancellationToken);

        var signedQuantity = request.MovementType == StockMovementType.AdjustmentOut ? -request.Quantity : request.Quantity;
        await EnsureIdentityTrackingAsync(productContext.Product, request.Quantity, request.LotNumber, request.SerialNumber);
        await EnsureNegativeStockPolicyAsync(
            productContext,
            request.WarehouseId,
            signedQuantity,
            request.LotNumber,
            request.SerialNumber,
            cancellationToken);

        var unitCost = signedQuantity < 0m
            ? await ResolveIssueUnitCostAsync(productContext, request.WarehouseId, request.LotNumber, request.SerialNumber, cancellationToken)
            : request.UnitCost > 0m ? request.UnitCost : productContext.Product.PurchasePrice;

        dbContext.StockMovements.Add(BuildStockMovement(
            tenantId,
            request.ProductId,
            request.WarehouseId,
            request.MovementDate,
            request.MovementType,
            signedQuantity,
            unitCost,
            string.IsNullOrWhiteSpace(request.ReferenceNumber) ? $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}" : request.ReferenceNumber.Trim(),
            request.LotNumber,
            request.SerialNumber,
            request.ExpirationDate));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateStockIssuesAsync(Guid tenantId, CommercialDocument deliveryNote, CancellationToken cancellationToken = default)
    {
        if (!deliveryNote.WarehouseId.HasValue)
        {
            return;
        }

        await EnsureWarehouseExistsAsync(tenantId, deliveryNote.WarehouseId.Value, cancellationToken);

        foreach (var line in deliveryNote.Lines.Where(x => x.ProductId.HasValue))
        {
            var productContext = await TryGetTrackedProductContextAsync(tenantId, line.ProductId!.Value, cancellationToken);
            if (productContext is null)
            {
                continue;
            }

            await EnsureIdentityTrackingAsync(productContext.Product, line.Quantity, line.LotNumber, line.SerialNumber);
            await EnsureNegativeStockPolicyAsync(
                productContext,
                deliveryNote.WarehouseId.Value,
                -line.Quantity,
                line.LotNumber,
                line.SerialNumber,
                cancellationToken);

            var unitCost = await ResolveIssueUnitCostAsync(productContext, deliveryNote.WarehouseId.Value, line.LotNumber, line.SerialNumber, cancellationToken);
            dbContext.StockMovements.Add(BuildStockMovement(
                tenantId,
                productContext.Product.Id,
                deliveryNote.WarehouseId.Value,
                deliveryNote.DocumentDate,
                StockMovementType.Issue,
                -line.Quantity,
                unitCost,
                deliveryNote.Number,
                line.LotNumber,
                line.SerialNumber,
                line.ExpirationDate));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateStockReceiptsAsync(Guid tenantId, CommercialDocument goodsReceipt, CancellationToken cancellationToken = default)
    {
        if (!goodsReceipt.WarehouseId.HasValue)
        {
            return;
        }

        await EnsureWarehouseExistsAsync(tenantId, goodsReceipt.WarehouseId.Value, cancellationToken);

        foreach (var line in goodsReceipt.Lines.Where(x => x.ProductId.HasValue))
        {
            var productContext = await TryGetTrackedProductContextAsync(tenantId, line.ProductId!.Value, cancellationToken);
            if (productContext is null)
            {
                continue;
            }

            await EnsureIdentityTrackingAsync(productContext.Product, line.Quantity, line.LotNumber, line.SerialNumber);

            dbContext.StockMovements.Add(BuildStockMovement(
                tenantId,
                productContext.Product.Id,
                goodsReceipt.WarehouseId.Value,
                goodsReceipt.DocumentDate,
                StockMovementType.Receipt,
                line.Quantity,
                line.UnitPriceExcludingTax,
                goodsReceipt.Number,
                line.LotNumber,
                line.SerialNumber,
                line.ExpirationDate));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ProductStockContext> GetTrackedProductContextAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .FirstOrDefaultAsync(x => x.Id == productId && x.TenantId == tenantId, cancellationToken);

        if (product is null || !product.TrackStock)
        {
            throw new NotFoundException("StockableProduct", productId);
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstAsync(x => x.Id == tenantId, cancellationToken);

        return new ProductStockContext(product, tenant);
    }

    private async Task<ProductStockContext?> TryGetTrackedProductContextAsync(Guid tenantId, Guid productId, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .FirstOrDefaultAsync(x => x.Id == productId && x.TenantId == tenantId, cancellationToken);

        if (product is null || !product.TrackStock)
        {
            return null;
        }

        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstAsync(x => x.Id == tenantId, cancellationToken);

        return new ProductStockContext(product, tenant);
    }

    private async Task EnsureWarehouseExistsAsync(Guid tenantId, Guid warehouseId, CancellationToken cancellationToken)
    {
        var warehouseExists = await dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(x => x.Id == warehouseId && x.TenantId == tenantId, cancellationToken);

        if (!warehouseExists)
        {
            throw new NotFoundException("Warehouse", warehouseId);
        }
    }

    private async Task ValidateStockDocumentWarehousesAsync(Guid tenantId, StockDocument document, CancellationToken cancellationToken)
    {
        switch (document.DocumentType)
        {
            case StockDocumentType.Entry:
                if (!document.DestinationWarehouseId.HasValue)
                {
                    throw new BusinessRuleException(
                        "Selectionne le depot de destination pour une entree de stock.",
                        errorCode: "STOCK_ENTRY_DESTINATION_REQUIRED");
                }

                await EnsureWarehouseExistsAsync(tenantId, document.DestinationWarehouseId.Value, cancellationToken);
                break;

            case StockDocumentType.Exit:
                if (!document.SourceWarehouseId.HasValue)
                {
                    throw new BusinessRuleException(
                        "Selectionne le depot source pour une sortie de stock.",
                        errorCode: "STOCK_EXIT_SOURCE_REQUIRED");
                }

                await EnsureWarehouseExistsAsync(tenantId, document.SourceWarehouseId.Value, cancellationToken);
                break;

            case StockDocumentType.Transfer:
                if (!document.SourceWarehouseId.HasValue || !document.DestinationWarehouseId.HasValue)
                {
                    throw new BusinessRuleException(
                        "Selectionne les depots source et destination pour un transfert.",
                        errorCode: "STOCK_TRANSFER_BOTH_WAREHOUSES_REQUIRED");
                }

                if (document.SourceWarehouseId == document.DestinationWarehouseId)
                {
                    throw new BusinessRuleException(
                        "Le depot source et le depot destination doivent etre differents pour un transfert.",
                        errorCode: "STOCK_TRANSFER_SAME_WAREHOUSE");
                }

                await EnsureWarehouseExistsAsync(tenantId, document.SourceWarehouseId.Value, cancellationToken);
                await EnsureWarehouseExistsAsync(tenantId, document.DestinationWarehouseId.Value, cancellationToken);
                break;
        }
    }

    private static Task EnsureIdentityTrackingAsync(Product product, decimal quantity, string? lotNumber, string? serialNumber)
    {
        if (product.StockIdentityTrackingMode == StockIdentityTrackingMode.Lot && string.IsNullOrWhiteSpace(lotNumber))
        {
            var ex = new BusinessRuleException(
                $"L'article {product.Sku} est gere par lot. Renseigne un numero de lot.",
                errorCode: "STOCK_LOT_NUMBER_REQUIRED");
            ex.Data["sku"] = product.Sku;
            throw ex;
        }

        if (product.StockIdentityTrackingMode == StockIdentityTrackingMode.SerialNumber)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                var ex = new BusinessRuleException(
                    $"L'article {product.Sku} est gere par numero de serie. Renseigne les numeros de serie pendant la saisie du document, soit un par un, soit par enumeration, soit par plage.",
                    errorCode: "STOCK_SERIAL_NUMBER_REQUIRED");
                ex.Data["sku"] = product.Sku;
                throw ex;
            }

            if (quantity != 1m)
            {
                var ex = new BusinessRuleException(
                    $"L'article {product.Sku} gere par numero de serie doit etre ventile avec une quantite de 1 par numero. Utilise l'enumeration ou la plage pour generer une ligne par serie.",
                    errorCode: "STOCK_SERIAL_QUANTITY_MUST_BE_ONE");
                ex.Data["sku"] = product.Sku;
                throw ex;
            }
        }

        return Task.CompletedTask;
    }

    private async Task EnsureNegativeStockPolicyAsync(
        ProductStockContext productContext,
        Guid warehouseId,
        decimal signedQuantity,
        string? lotNumber,
        string? serialNumber,
        CancellationToken cancellationToken)
    {
        if (signedQuantity >= 0m || productContext.Tenant.AllowNegativeStock)
        {
            return;
        }

        var available = await GetOnHandQuantityAsync(productContext.Product.Id, warehouseId, lotNumber, serialNumber, cancellationToken);
        if (available + signedQuantity < 0m)
        {
            var ex = new BusinessRuleException(
                $"Stock insuffisant pour {productContext.Product.Sku}. Disponible : {available}.",
                errorCode: "STOCK_INSUFFICIENT");
            ex.Data["sku"] = productContext.Product.Sku;
            ex.Data["available"] = available;
            ex.Data["requested"] = -signedQuantity;
            throw ex;
        }
    }

    private async Task<decimal> ResolveIssueUnitCostAsync(
        ProductStockContext productContext,
        Guid warehouseId,
        string? lotNumber,
        string? serialNumber,
        CancellationToken cancellationToken)
    {
        var movements = await dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.TenantId == productContext.Tenant.Id && x.ProductId == productContext.Product.Id && x.WarehouseId == warehouseId)
            .OrderBy(x => x.MovementDate)
            .ThenBy(x => x.CreatedOnUtc)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            var serialMovement = movements
                .Where(x => string.Equals(x.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase) && x.Quantity > 0m)
                .OrderByDescending(x => x.MovementDate)
                .ThenByDescending(x => x.CreatedOnUtc)
                .FirstOrDefault();

            if (serialMovement is not null)
            {
                return serialMovement.UnitCost;
            }
        }

        if (!string.IsNullOrWhiteSpace(lotNumber))
        {
            movements = movements
                .Where(x => string.Equals(x.LotNumber, lotNumber, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return ResolveIssueUnitCost(productContext.Product, movements);
    }

    private static decimal ResolveIssueUnitCost(Product product, IReadOnlyList<StockMovement> movements)
    {
        if (movements.Count == 0)
        {
            return product.PurchasePrice;
        }

        return product.StockValuationMethod switch
        {
            StockValuationMethod.Fifo => ResolveFifoUnitCost(movements, product.PurchasePrice),
            StockValuationMethod.LastPurchaseCost => movements
                .Where(x => x.Quantity > 0m)
                .OrderByDescending(x => x.MovementDate)
                .ThenByDescending(x => x.CreatedOnUtc)
                .Select(x => x.UnitCost)
                .FirstOrDefault(product.PurchasePrice),
            _ => ResolveAverageUnitCost(movements, product.PurchasePrice)
        };
    }

    private static decimal ResolveAverageUnitCost(IReadOnlyList<StockMovement> movements, decimal fallback)
    {
        var quantity = movements.Sum(x => x.Quantity);
        var value = movements.Sum(x => x.Quantity * x.UnitCost);
        return quantity > 0m ? decimal.Round(value / quantity, 2) : fallback;
    }

    private static decimal ResolveFifoUnitCost(IReadOnlyList<StockMovement> movements, decimal fallback)
    {
        var layers = new Queue<(decimal Quantity, decimal Cost)>();

        foreach (var movement in movements.OrderBy(x => x.MovementDate).ThenBy(x => x.CreatedOnUtc))
        {
            if (movement.Quantity > 0m)
            {
                layers.Enqueue((movement.Quantity, movement.UnitCost));
                continue;
            }

            var remainingIssue = -movement.Quantity;
            while (remainingIssue > 0m && layers.Count > 0)
            {
                var layer = layers.Dequeue();
                if (layer.Quantity > remainingIssue)
                {
                    layers.Enqueue((layer.Quantity - remainingIssue, layer.Cost));
                    remainingIssue = 0m;
                }
                else
                {
                    remainingIssue -= layer.Quantity;
                }
            }
        }

        return layers.Count > 0 ? layers.Peek().Cost : fallback;
    }

    private async Task<decimal> GetOnHandQuantityAsync(Guid productId, Guid warehouseId, string? lotNumber, string? serialNumber, CancellationToken cancellationToken)
    {
        var query = dbContext.StockMovements
            .AsNoTracking()
            .Where(x => x.ProductId == productId && x.WarehouseId == warehouseId);

        if (!string.IsNullOrWhiteSpace(lotNumber))
        {
            query = query.Where(x => x.LotNumber == lotNumber);
        }

        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            query = query.Where(x => x.SerialNumber == serialNumber);
        }

        return await query.SumAsync(x => (decimal?)x.Quantity, cancellationToken) ?? 0m;
    }

    private static StockMovement BuildStockMovement(
        Guid tenantId,
        Guid productId,
        Guid warehouseId,
        DateOnly movementDate,
        StockMovementType movementType,
        decimal quantity,
        decimal unitCost,
        string referenceNumber,
        string? lotNumber,
        string? serialNumber,
        DateOnly? expirationDate) =>
        new()
        {
            TenantId = tenantId,
            ProductId = productId,
            WarehouseId = warehouseId,
            MovementDate = movementDate,
            MovementType = movementType,
            Quantity = quantity,
            UnitCost = unitCost,
            ReferenceNumber = referenceNumber,
            LotNumber = string.IsNullOrWhiteSpace(lotNumber) ? null : lotNumber.Trim().ToUpperInvariant(),
            SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim().ToUpperInvariant(),
            ExpirationDate = expirationDate
        };

    private sealed record ProductStockContext(Product Product, Domain.Entities.SaaS.Tenant Tenant);
}
