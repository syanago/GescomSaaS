namespace GescomSaas.Application.Models;

public sealed record WarehouseValueItem(
    string WarehouseCode,
    string WarehouseLabel,
    decimal TotalQuantity,
    decimal TotalValue);
