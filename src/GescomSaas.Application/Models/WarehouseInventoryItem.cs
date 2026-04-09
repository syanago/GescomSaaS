namespace GescomSaas.Application.Models;

public sealed record WarehouseInventoryItem(
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseLabel,
    decimal TotalQuantity,
    decimal TotalValue);
