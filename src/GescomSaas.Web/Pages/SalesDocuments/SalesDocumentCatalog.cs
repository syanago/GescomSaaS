using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.SalesDocuments;

public static class SalesDocumentCatalog
{
    public static IReadOnlyList<CommercialDocumentType> SalesTypes { get; } =
    [
        CommercialDocumentType.SalesQuote,
        CommercialDocumentType.SalesOrder,
        CommercialDocumentType.DeliveryNote,
        CommercialDocumentType.SalesInvoice,
        CommercialDocumentType.SalesCreditNote
    ];

    public static string Label(CommercialDocumentType type) => type switch
    {
        CommercialDocumentType.SalesQuote => "Devis",
        CommercialDocumentType.SalesOrder => "Commande client",
        CommercialDocumentType.DeliveryNote => "Bon de livraison",
        CommercialDocumentType.SalesInvoice => "Facture",
        CommercialDocumentType.SalesCreditNote => "Avoir client",
        _ => type.ToString()
    };

    public static CommercialDocumentType Normalize(string? type) =>
        Enum.TryParse<CommercialDocumentType>(type, true, out var parsed) && SalesTypes.Contains(parsed)
            ? parsed
            : CommercialDocumentType.SalesQuote;

    public static IReadOnlyList<CommercialDocumentType> GetTargets(CommercialDocumentType sourceType) => sourceType switch
    {
        CommercialDocumentType.SalesQuote => [CommercialDocumentType.SalesOrder],
        CommercialDocumentType.SalesOrder => [CommercialDocumentType.DeliveryNote, CommercialDocumentType.SalesInvoice],
        CommercialDocumentType.DeliveryNote => [CommercialDocumentType.SalesInvoice],
        CommercialDocumentType.SalesInvoice => [CommercialDocumentType.SalesCreditNote],
        _ => []
    };
}
