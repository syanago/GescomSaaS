namespace GescomSaas.Application.Models;

public sealed record SalesDashboardSnapshot(
    int OpenQuoteCount,
    int OpenOrderCount,
    int InvoiceCountThisMonth,
    int CreditNoteCountThisMonth,
    decimal RevenueThisMonth,
    decimal OpenReceivables,
    decimal OverdueReceivables,
    int OverdueReceivableCount)
{
    public static SalesDashboardSnapshot Empty { get; } = new(0, 0, 0, 0, 0m, 0m, 0m, 0);
}
