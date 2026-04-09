namespace GescomSaas.Application.Models;

public sealed record FinanceDashboardSnapshot(
    int ReceivableCount,
    decimal ReceivableBalance,
    int OverdueReceivableCount,
    decimal OverdueReceivableBalance,
    int PayableCount,
    decimal PayableBalance,
    int OverduePayableCount,
    decimal OverduePayableBalance,
    IReadOnlyList<RecentPaymentDashboardItem> RecentPayments,
    IReadOnlyList<RecentReminderDashboardItem> RecentReminders)
{
    public static FinanceDashboardSnapshot Empty { get; } = new(0, 0m, 0, 0m, 0, 0m, 0, 0m, [], []);
}
