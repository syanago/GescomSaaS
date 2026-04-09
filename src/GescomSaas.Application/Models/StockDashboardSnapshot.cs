namespace GescomSaas.Application.Models;

public sealed record StockDashboardSnapshot(
    int TrackedProductCount,
    decimal TotalOnHandQuantity,
    decimal TotalStockValue,
    int WatchItemCount,
    IReadOnlyList<StockWatchItem> WatchItems,
    IReadOnlyList<WarehouseValueItem> TopWarehouses)
{
    public static StockDashboardSnapshot Empty { get; } = new(0, 0m, 0m, 0, [], []);
}
