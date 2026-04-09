using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Contracts;

public interface ISettlementService
{
    Task<IReadOnlyList<OpenItemSummary>> GetOpenItemsAsync(Guid tenantId, PaymentDirection direction, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentHistoryItem>> GetPaymentsAsync(Guid tenantId, PaymentDirection? direction = null, CancellationToken cancellationToken = default);
    Task RegisterPaymentAsync(Guid tenantId, PaymentRegistrationRequest request, CancellationToken cancellationToken = default);
    Task RegisterReminderAsync(Guid tenantId, Guid documentId, ReminderLevel level, string? notes, CancellationToken cancellationToken = default);
}
