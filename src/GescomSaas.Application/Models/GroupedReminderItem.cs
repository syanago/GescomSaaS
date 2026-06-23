namespace GescomSaas.Application.Models;

public sealed record GroupedReminderItem(
    Guid PartnerId,
    string PartnerCode,
    string PartnerName,
    int DocumentCount,
    decimal TotalBalanceAmount,
    int MaxOverdueDays,
    IReadOnlyList<ReminderQueueItem> Items);
