namespace GescomSaas.Application.Models;

public sealed record PlatformAdminDashboardSnapshot(
    int TotalTenants,
    int ActiveTenants,
    int TrialTenants,
    decimal MonthlyRecurringRevenue,
    int OverdueInvoices,
    int TenantsWithQuotaAlerts,
    int WarningNotifications,
    int CriticalNotifications,
    IReadOnlyList<QuotaNotificationItem> RecentQuotaNotifications,
    IReadOnlyList<TenantAdminSummary> AlertedTenants,
    IReadOnlyList<PlatformInvoiceSummaryItem> RecentInvoices)
{
    public static PlatformAdminDashboardSnapshot Empty { get; } = new(0, 0, 0, 0m, 0, 0, 0, 0, [], [], []);
}
