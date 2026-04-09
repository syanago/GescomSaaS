using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record QuotaNotificationItem(
    Guid NotificationId,
    Guid TenantId,
    string TenantName,
    string PlanName,
    string QuotaLabel,
    PlatformNotificationSeverity Severity,
    string Title,
    string Message,
    int Used,
    int Limit,
    DateTime LastTriggeredOnUtc);
