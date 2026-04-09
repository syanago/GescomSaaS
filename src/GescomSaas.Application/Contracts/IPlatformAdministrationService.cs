using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.SaaS;

namespace GescomSaas.Application.Contracts;

public interface IPlatformAdministrationService
{
    Task<PlatformAdminDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantAdminSummary>> GetTenantSummariesAsync(CancellationToken cancellationToken = default);
    Task<PlatformInvoice> GeneratePlatformInvoiceAsync(Guid tenantId, DateOnly issueDate, DateOnly dueDate, CancellationToken cancellationToken = default);
    Task AcknowledgeQuotaNotificationAsync(Guid notificationId, CancellationToken cancellationToken = default);
    void RecalculateInvoiceTotals(PlatformInvoice invoice);
}
