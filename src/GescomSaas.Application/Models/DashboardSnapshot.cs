using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record DashboardSnapshot(
    string TenantName,
    string PlanName,
    SubscriptionStatus SubscriptionStatus,
    DateOnly ReportingDate,
    string ReportingPeriodLabel,
    IReadOnlyList<QuotaUsageItem> Quotas,
    IReadOnlyList<DashboardMetric> Metrics,
    SalesDashboardSnapshot Sales,
    PurchaseDashboardSnapshot Purchases,
    FinanceDashboardSnapshot Finance,
    StockDashboardSnapshot Stock,
    IReadOnlyList<RecentDocumentItem> RecentDocuments,
    IReadOnlyList<FeatureModule> Modules)
{
    public static DashboardSnapshot Empty(IReadOnlyList<FeatureModule> modules) =>
        new(
            "Aucun tenant initialise",
            "Aucun plan",
            SubscriptionStatus.Trial,
            DateOnly.FromDateTime(DateTime.UtcNow),
            string.Empty,
            [],
            [],
            SalesDashboardSnapshot.Empty,
            PurchaseDashboardSnapshot.Empty,
            FinanceDashboardSnapshot.Empty,
            StockDashboardSnapshot.Empty,
            [],
            modules);
}
