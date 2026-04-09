using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record RecentReminderDashboardItem(
    DateTime SentOnUtc,
    string DocumentNumber,
    string PartnerName,
    ReminderLevel Level,
    string Channel,
    string? Notes);
