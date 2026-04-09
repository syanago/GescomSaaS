using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Finance;

public static class FinanceScope
{
    public const string Receivables = "receivables";
    public const string Payables = "payables";

    public static string Normalize(string? scope) =>
        string.Equals(scope, Payables, StringComparison.OrdinalIgnoreCase) ? Payables : Receivables;

    public static PaymentDirection ToDirection(string scope) =>
        Normalize(scope) == Payables ? PaymentDirection.Outgoing : PaymentDirection.Incoming;

    public static string Title(string scope) =>
        Normalize(scope) == Payables ? "Echeancier fournisseurs" : "Echeancier clients";

    public static string PaymentTitle(string scope) =>
        Normalize(scope) == Payables ? "Decaissements" : "Encaissements";
}
