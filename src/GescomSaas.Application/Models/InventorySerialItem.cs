namespace GescomSaas.Application.Models;

public sealed record InventorySerialItem(
    Guid ProductId,
    string ProductCode,
    string ProductLabel,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseLabel,
    string SerialNumber,
    decimal OnHandQuantity,
    decimal UnitCost,
    decimal StockValue,
    DateOnly LastMovementDate);
