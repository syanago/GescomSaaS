using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.PurchaseDocuments;

public static class PurchaseDocumentCatalog
{
    public static IReadOnlyList<CommercialDocumentType> PurchaseTypes { get; } =
    [
        CommercialDocumentType.PurchaseRequest,
        CommercialDocumentType.PurchaseOrder,
        CommercialDocumentType.GoodsReceipt,
        CommercialDocumentType.PurchaseInvoice,
        CommercialDocumentType.SupplierCreditNote
    ];

    public static string Label(CommercialDocumentType type) => type switch
    {
        CommercialDocumentType.PurchaseRequest => "Demande d'achat",
        CommercialDocumentType.PurchaseOrder => "Commande fournisseur",
        CommercialDocumentType.GoodsReceipt => "Reception",
        CommercialDocumentType.PurchaseInvoice => "Facture fournisseur",
        CommercialDocumentType.SupplierCreditNote => "Avoir fournisseur",
        _ => type.ToString()
    };

    public static CommercialDocumentType Normalize(string? type) =>
        Enum.TryParse<CommercialDocumentType>(type, true, out var parsed) && PurchaseTypes.Contains(parsed)
            ? parsed
            : CommercialDocumentType.PurchaseRequest;

    public static IReadOnlyList<CommercialDocumentType> GetTargets(CommercialDocumentType sourceType) => sourceType switch
    {
        CommercialDocumentType.PurchaseRequest => [CommercialDocumentType.PurchaseOrder],
        CommercialDocumentType.PurchaseOrder => [CommercialDocumentType.GoodsReceipt, CommercialDocumentType.PurchaseInvoice],
        CommercialDocumentType.GoodsReceipt => [CommercialDocumentType.PurchaseInvoice],
        CommercialDocumentType.PurchaseInvoice => [CommercialDocumentType.SupplierCreditNote],
        _ => []
    };
}
