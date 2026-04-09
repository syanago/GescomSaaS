using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface IPlatformInvoicePdfService
{
    Task<CommercialDocumentPdfResult> GeneratePdfAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
