using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record StockAdjustmentRequest(
    Guid ProductId,
    Guid WarehouseId,
    DateOnly MovementDate,
    StockMovementType MovementType,
    decimal Quantity,
    decimal UnitCost,
    string? ReferenceNumber);
