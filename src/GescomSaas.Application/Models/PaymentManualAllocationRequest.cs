namespace GescomSaas.Application.Models;

public sealed record PaymentManualAllocationRequest(
    Guid PaymentId,
    Guid CommercialDocumentId,
    decimal AllocatedAmount,
    string? Notes);
