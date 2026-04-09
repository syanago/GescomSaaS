namespace GescomSaas.Application.Models;

public sealed record InventoryStockItem(
    Guid ProductId,
    string Sku,
    string Label,
    string UnitOfMeasure,
    decimal OnHandQuantity,
    decimal AverageUnitCost,
    decimal StockValue);
