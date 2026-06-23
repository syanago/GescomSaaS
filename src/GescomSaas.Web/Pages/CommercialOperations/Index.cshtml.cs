using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages.CommercialOperations;

[Authorize]
public class IndexModel(IUserPermissionService userPermissionService) : PageModel
{
    private static readonly IReadOnlyList<string> RequiredPermissions =
    [
        TenantPermissionKeys.SalesDocumentsManage,
        TenantPermissionKeys.PurchasesDocumentsManage,
        TenantPermissionKeys.FinanceManage,
        TenantPermissionKeys.InventoryManage
    ];

    public IReadOnlyList<CommercialModuleCard> Cards { get; private set; } = [];
    public IReadOnlyList<CommercialFlowItem> Flows { get; private set; } = [];
    public IReadOnlyList<CommercialModuleCard> QuickLinks { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await userPermissionService.HasAnyPermissionAsync(User, RequiredPermissions, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var permissions = await userPermissionService.GetCurrentPermissionKeysAsync(HttpContext.RequestAborted);

        List<CommercialModuleCard> cards = [];
        List<CommercialFlowItem> flows = [];
        List<CommercialModuleCard> quickLinks = [];

        if (permissions.Contains(TenantPermissionKeys.SalesDocumentsManage, StringComparer.OrdinalIgnoreCase))
        {
            cards.Add(new("Ventes", "Devis, commandes, livraisons, factures et avoirs clients.", "/SalesDocuments/Index"));
            flows.Add(new("Cycle de vente", "Suivez vos pieces depuis le devis jusqu'a la facture ou a l'avoir, avec generation des bons de livraison."));
            quickLinks.Add(new("Nouveau devis", "Demarrer une proposition commerciale pour un client.", "/SalesDocuments/Create?type=SalesQuote"));
        }

        if (permissions.Contains(TenantPermissionKeys.PurchasesDocumentsManage, StringComparer.OrdinalIgnoreCase))
        {
            cards.Add(new("Achats", "Demandes, commandes fournisseurs, receptions, factures et avoirs fournisseurs.", "/PurchaseDocuments/Index"));
            flows.Add(new("Cycle d'achat", "Pilotez les demandes internes, les commandes fournisseurs, les receptions et la facturation d'achat."));
            quickLinks.Add(new("Nouvelle commande fournisseur", "Initier un achat aupres d'un fournisseur.", "/PurchaseDocuments/Create?type=PurchaseOrder"));
        }

        if (permissions.Contains(TenantPermissionKeys.FinanceManage, StringComparer.OrdinalIgnoreCase))
        {
            cards.Add(new("Finances", "Echeanciers, reglements, soldes ouverts et relances.", "/Finance/OpenItems"));
            flows.Add(new("Cycle financier", "Enregistrez les reglements, suivez les echeances et declenchez les relances sur les factures ouvertes."));
            quickLinks.Add(new("Enregistrer un reglement", "Affecter un encaissement ou un decaissement a une piece ouverte.", "/Finance/RegisterPayment"));
        }

        if (permissions.Contains(TenantPermissionKeys.InventoryManage, StringComparer.OrdinalIgnoreCase))
        {
            cards.Add(new("Stock", "Inventaire, mouvements, ajustements, documents d'entree, sortie et transfert.", "/Inventory/Index"));
            flows.Add(new("Cycle stock", "Controlez les disponibilites, les mouvements par depot, les documents d'entree / sortie / transfert et la valorisation du stock."));
            quickLinks.Add(new("Document de stock", "Creer une entree, une sortie ou un transfert de stock.", "/StockDocuments/Index"));
            quickLinks.Add(new("Mouvements de stock", "Consulter l'historique detaille des entrees, sorties et ajustements.", "/Inventory/Movements"));
        }

        Cards = cards;
        Flows = flows;
        QuickLinks = quickLinks;
        return Page();
    }
}

public sealed record CommercialModuleCard(string Title, string Description, string Href);
public sealed record CommercialFlowItem(string Title, string Description);
