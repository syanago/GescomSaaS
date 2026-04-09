using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.StockDocuments;

public static class StockDocumentCatalog
{
    public static StockDocumentType Normalize(string? rawType) =>
        Enum.TryParse<StockDocumentType>(rawType, true, out var parsed) ? parsed : StockDocumentType.Entry;

    public static string Label(StockDocumentType documentType) => documentType switch
    {
        StockDocumentType.Entry => "Entree de stock",
        StockDocumentType.Exit => "Sortie de stock",
        StockDocumentType.Transfer => "Transfert de stock",
        _ => documentType.ToString()
    };

    public static string ShortLabel(StockDocumentType documentType) => documentType switch
    {
        StockDocumentType.Entry => "Entrees",
        StockDocumentType.Exit => "Sorties",
        StockDocumentType.Transfer => "Transferts",
        _ => documentType.ToString()
    };

    public static bool UsesSourceWarehouse(StockDocumentType documentType) =>
        documentType is StockDocumentType.Exit or StockDocumentType.Transfer;

    public static bool UsesDestinationWarehouse(StockDocumentType documentType) =>
        documentType is StockDocumentType.Entry or StockDocumentType.Transfer;
}
