namespace GescomSaas.Application.Models;

public sealed record PurchaseDashboardSnapshot(
    int OpenRequestCount,
    int OpenOrderCount,
    int InvoiceCountThisMonth,
    int CreditNoteCountThisMonth,
    decimal SpendThisMonth,
    decimal OpenPayables,
    decimal OverduePayables,
    int OverduePayableCount)
{
    public static PurchaseDashboardSnapshot Empty { get; } = new(0, 0, 0, 0, 0m, 0m, 0m, 0);
}
