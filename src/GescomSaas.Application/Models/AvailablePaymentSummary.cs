using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record AvailablePaymentSummary(
    Guid PaymentId,
    DateOnly PaymentDate,
    string ReferenceNumber,
    PaymentType Type,
    PaymentMethod Method,
    string CurrencyCode,
    decimal Amount,
    decimal AllocatedAmount,
    decimal AvailableAmount);
