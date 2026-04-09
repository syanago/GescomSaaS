using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Contracts;

public interface IStockDocumentService
{
    Task<StockDocument> InitializeDraftAsync(Guid tenantId, StockDocumentType documentType, CancellationToken cancellationToken = default);
    Task PostAsync(Guid tenantId, Guid stockDocumentId, CancellationToken cancellationToken = default);
}
