using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record PaymentHistoryItem(
    Guid PaymentId,
    DateOnly PaymentDate,
    string ReferenceNumber,
    string PartnerName,
    PaymentDirection Direction,
    PaymentMethod Method,
    string CurrencyCode,
    decimal Amount,
    int AllocationCount);
