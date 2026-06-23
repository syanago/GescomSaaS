using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record OpenItemSummary(
    Guid DocumentId,
    Guid? PartnerId,
    string? PartnerCode,
    string Number,
    string PartnerName,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    string CurrencyCode,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal BalanceAmount,
    int OverdueDays,
    CommercialDocumentStatus Status,
    CommercialPaymentStatus PaymentStatus,
    bool InDispute,
    DateOnly? PromiseToPayDate,
    ReminderLevel? LastReminderLevel,
    DateTime? LastReminderOnUtc);
