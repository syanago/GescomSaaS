using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface ICommercialDocumentPdfService
{
    Task<CommercialDocumentPdfResult> GeneratePdfAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default);
}
