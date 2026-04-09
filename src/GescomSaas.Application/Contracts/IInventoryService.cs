using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Contracts;

public interface IInventoryService
{
    Task<InventoryDashboardSnapshot> GetDashboardAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryMovementItem>> GetMovementsAsync(Guid tenantId, Guid? productId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryLotItem>> GetLotStocksAsync(Guid tenantId, Guid? productId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventorySerialItem>> GetSerialStocksAsync(Guid tenantId, Guid? productId = null, Guid? warehouseId = null, CancellationToken cancellationToken = default);
    Task PostStockDocumentAsync(Guid tenantId, StockDocument document, CancellationToken cancellationToken = default);
    Task RegisterAdjustmentAsync(Guid tenantId, StockAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task CreateStockIssuesAsync(Guid tenantId, CommercialDocument deliveryNote, CancellationToken cancellationToken = default);
    Task CreateStockReceiptsAsync(Guid tenantId, CommercialDocument goodsReceipt, CancellationToken cancellationToken = default);
}
