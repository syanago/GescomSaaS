using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Inventory;

public static class StockTrackingCatalog
{
    public static string Label(StockIdentityTrackingMode mode) => mode switch
    {
        StockIdentityTrackingMode.Lot => "Par lot",
        StockIdentityTrackingMode.SerialNumber => "Par numero de serie",
        _ => "Aucun"
    };

    public static string Hint(StockIdentityTrackingMode mode, string? productCode = null) => mode switch
    {
        StockIdentityTrackingMode.Lot => $"{ProductLabel(productCode)} gere par lot. Renseigne le lot et, si besoin, la peremption.",
        StockIdentityTrackingMode.SerialNumber => $"{ProductLabel(productCode)} gere par numero de serie. Renseigne un numero unique et une quantite de 1.",
        _ => $"{ProductLabel(productCode)} n'utilise pas de gestion lot / serie."
    };

    private static string ProductLabel(string? productCode) =>
        string.IsNullOrWhiteSpace(productCode) ? "Cet article" : $"L'article {productCode}";
}
