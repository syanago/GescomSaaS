using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface IInventoryService
{
    Task<InventoryDashboardSnapshot> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryMovementItem>> GetMovementsAsync(Guid tenantId, Guid? productId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task RegisterAdjustmentAsync(Guid tenantId, StockAdjustmentRequest request, CancellationToken cancellationToken = default);
}
