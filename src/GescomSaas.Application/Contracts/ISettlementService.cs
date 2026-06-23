using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Contracts;

public interface ISettlementService
{
    Task<IReadOnlyList<OpenItemSummary>> GetOpenItemsAsync(Guid tenantId, PaymentDirection direction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentHistoryItem>> GetPaymentsAsync(Guid tenantId, PaymentDirection? direction = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AvailablePaymentSummary>> GetAvailablePaymentsAsync(Guid tenantId, Guid partnerId, PaymentDirection direction, CancellationToken cancellationToken = default);
    Task<CustomerAccountSummary?> GetCustomerAccountAsync(Guid tenantId, Guid partnerId, PaymentDirection direction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReminderQueueItem>> GetReminderQueueAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task RegisterPaymentAsync(Guid tenantId, PaymentRegistrationRequest request, CancellationToken cancellationToken = default);
    Task AllocatePaymentAsync(Guid tenantId, PaymentManualAllocationRequest request, CancellationToken cancellationToken = default);
    Task<DepositApplicationResult> ApplyAvailableDepositsAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default);
    Task<OfflinePaymentApplyResult> UpsertOfflinePaymentAsync(Guid tenantId, OfflinePaymentSyncItem item, CancellationToken cancellationToken = default);
    Task ReplaceOfflineAllocationsAsync(Guid tenantId, Guid paymentId, IReadOnlyList<OfflinePaymentAllocationSyncItem> allocations, CancellationToken cancellationToken = default);
    Task RegisterReminderAsync(Guid tenantId, Guid documentId, ReminderLevel level, string? notes, CancellationToken cancellationToken = default);
    Task RegisterGroupedReminderAsync(Guid tenantId, Guid partnerId, string? notes, CancellationToken cancellationToken = default);
    Task SetDisputeStateAsync(Guid tenantId, Guid documentId, bool inDispute, string? notes, CancellationToken cancellationToken = default);
    Task SetPromiseToPayAsync(Guid tenantId, Guid documentId, DateOnly? promiseToPayDate, string? notes, CancellationToken cancellationToken = default);
    Task EnsureSalesDocumentAllowedAsync(Guid tenantId, Guid partnerId, CommercialDocumentType documentType, CancellationToken cancellationToken = default);
}
