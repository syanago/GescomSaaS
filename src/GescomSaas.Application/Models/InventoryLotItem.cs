namespace GescomSaas.Application.Models;

public sealed record InventoryLotItem(
    Guid ProductId,
    string ProductCode,
    string ProductLabel,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseLabel,
    string LotNumber,
    DateOnly? ExpirationDate,
    decimal OnHandQuantity,
    decimal AverageUnitCost,
    decimal StockValue,
    DateOnly LastMovementDate);
