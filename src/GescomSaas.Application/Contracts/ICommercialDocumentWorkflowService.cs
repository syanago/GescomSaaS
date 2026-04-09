using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Contracts;

public interface ICommercialDocumentWorkflowService
{
    Task<CommercialDocument> InitializeDraftAsync(Guid tenantId, CommercialDocumentType documentType, CancellationToken cancellationToken = default);
    Task<CommercialDocument> CreateFromSourceAsync(Guid tenantId, Guid sourceDocumentId, CommercialDocumentType targetDocumentType, CancellationToken cancellationToken = default);
    void RecalculateTotals(CommercialDocument document);
    Task SynchronizeSourceStatusAsync(Guid sourceDocumentId, CancellationToken cancellationToken = default);
}
