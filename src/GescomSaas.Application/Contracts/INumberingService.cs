using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Contracts;

public interface INumberingService
{
    Task<NumberingRuleSnapshot> GetDocumentRuleAsync(Guid tenantId, CommercialDocumentType documentType, CancellationToken cancellationToken = default);
    Task<string> ResolveDocumentNumberAsync(Guid tenantId, CommercialDocumentType documentType, string? requestedValue, CancellationToken cancellationToken = default);
    Task<NumberingRuleSnapshot> GetReferenceRuleAsync(Guid tenantId, ReferenceNumberingScope scope, CancellationToken cancellationToken = default);
    Task<string> ResolveReferenceCodeAsync(Guid tenantId, ReferenceNumberingScope scope, string? requestedValue, CancellationToken cancellationToken = default);
}

public sealed record NumberingRuleSnapshot(NumberingMode Mode, string Prefix, int NumberLength, int NextValue, string Preview);
