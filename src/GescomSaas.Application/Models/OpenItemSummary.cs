using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record OpenItemSummary(
    Guid DocumentId,
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
    ReminderLevel? LastReminderLevel,
    DateTime? LastReminderOnUtc);
