using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages.CommercialOperations;

[Authorize]
public class IndexModel : PageModel
{
    public IReadOnlyList<CommercialModuleCard> Cards { get; } =
    [
        new("Ventes", "Devis, commandes, livraisons, factures et avoirs clients.", "/SalesDocuments/Index"),
        new("Achats", "Demandes, commandes fournisseurs, receptions, factures et avoirs fournisseurs.", "/PurchaseDocuments/Index"),
        new("Finances", "Echeanciers, reglements, soldes ouverts et relances.", "/Finance/OpenItems"),
        new("Stock", "Inventaire, mouvements, ajustements et valorisation.", "/Inventory/Index")
    ];

    public IReadOnlyList<CommercialFlowItem> Flows { get; } =
    [
        new("Cycle de vente", "Suivez vos pieces depuis le devis jusqu'a la facture ou a l'avoir, avec generation des bons de livraison."),
        new("Cycle d'achat", "Pilotez les demandes internes, les commandes fournisseurs, les receptions et la facturation d'achat."),
        new("Cycle financier", "Enregistrez les reglements, suivez les echeances et declenchez les relances sur les factures ouvertes."),
        new("Cycle stock", "Controlez les disponibilites, les mouvements par depot et la valorisation du stock.")
    ];

    public IReadOnlyList<CommercialModuleCard> QuickLinks { get; } =
    [
        new("Nouveau devis", "Demarrer une proposition commerciale pour un client.", "/SalesDocuments/Create?type=SalesQuote"),
        new("Nouvelle commande fournisseur", "Initier un achat aupres d'un fournisseur.", "/PurchaseDocuments/Create?type=PurchaseOrder"),
        new("Enregistrer un reglement", "Affecter un encaissement ou un decaissement a une piece ouverte.", "/Finance/RegisterPayment"),
        new("Mouvements de stock", "Consulter l'historique detaille des entrees, sorties et ajustements.", "/Inventory/Movements")
    ];
}

public sealed record CommercialModuleCard(string Title, string Description, string Href);
public sealed record CommercialFlowItem(string Title, string Description);
