using GescomSaas.Domain.Enums;
using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface ITenantQuotaEnforcementService
{
    Task<IReadOnlyList<QuotaUsageItem>> GetQuotaUsageAsync(Guid tenantId, DateOnly? documentDate = null, CancellationToken cancellationToken = default);
    Task EnsureCanManageUsersAsync(Guid tenantId, int additionalUsers = 1, CancellationToken cancellationToken = default);
    Task EnsureCanCreatePartnerAsync(Guid tenantId, BusinessPartnerType partnerType, bool isActive, CancellationToken cancellationToken = default);
    Task EnsureCanUpdatePartnerAsync(Guid tenantId, Guid partnerId, BusinessPartnerType partnerType, bool isActive, CancellationToken cancellationToken = default);
    Task EnsureCanCreateProductAsync(Guid tenantId, bool isActive, CancellationToken cancellationToken = default);
    Task EnsureCanUpdateProductAsync(Guid tenantId, Guid productId, bool isActive, CancellationToken cancellationToken = default);
    Task EnsureCanCreateWarehouseAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureCanCreateDocumentAsync(Guid tenantId, DateOnly documentDate, CancellationToken cancellationToken = default);
}
