using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record CustomerAccountSummary(
    Guid PartnerId,
    string PartnerCode,
    string PartnerName,
    CustomerAccountStatus AccountStatus,
    decimal CreditLimit,
    decimal CreditRemaining,
    decimal OpenAmount,
    decimal OverdueAmount,
    int OldestOverdueDays,
    bool CreditLimitExceeded,
    bool CanCreateSalesOrder,
    bool CanCreateDelivery,
    decimal AvailableDepositAmount,
    decimal UnallocatedPaymentAmount,
    int OpenDocumentCount,
    int OverdueDocumentCount,
    int ReminderCount,
    IReadOnlyList<OpenItemSummary> OpenItems,
    IReadOnlyList<PaymentHistoryItem> RecentPayments,
    IReadOnlyList<AvailablePaymentSummary> AvailablePayments);
