namespace GescomSaas.Application.Models;

public sealed record InventoryDashboardSnapshot(
    int TrackedProductCount,
    decimal TotalOnHandQuantity,
    decimal TotalStockValue,
    IReadOnlyList<InventoryStockItem> ProductStocks,
    IReadOnlyList<WarehouseInventoryItem> WarehouseStocks);
