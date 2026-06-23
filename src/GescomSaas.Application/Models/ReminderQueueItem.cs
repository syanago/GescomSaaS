using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record ReminderQueueItem(
    Guid DocumentId,
    Guid PartnerId,
    string PartnerCode,
    string PartnerName,
    string Number,
    DateOnly DocumentDate,
    DateOnly? DueDate,
    string CurrencyCode,
    decimal BalanceAmount,
    int OverdueDays,
    ReminderLevel RecommendedLevel,
    ReminderLevel? LastReminderLevel,
    DateTime? LastReminderOnUtc,
    bool InDispute,
    DateOnly? PromiseToPayDate);
