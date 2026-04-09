using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record InventoryMovementItem(
    Guid MovementId,
    DateOnly MovementDate,
    string ProductCode,
    string ProductLabel,
    string WarehouseCode,
    string WarehouseLabel,
    StockMovementType MovementType,
    decimal Quantity,
    decimal UnitCost,
    decimal ExtendedValue,
    string? ReferenceNumber);
