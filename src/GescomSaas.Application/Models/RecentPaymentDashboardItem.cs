using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record RecentPaymentDashboardItem(
    DateOnly PaymentDate,
    string ReferenceNumber,
    string PartnerName,
    PaymentDirection Direction,
    PaymentMethod Method,
    decimal Amount,
    string CurrencyCode);
