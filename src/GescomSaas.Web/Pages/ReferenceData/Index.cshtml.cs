using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages.ReferenceData;

[Authorize]
public class IndexModel(IUserPermissionService userPermissionService) : PageModel
{
    private static readonly IReadOnlyList<string> RequiredPermissions =
    [
        TenantPermissionKeys.ReferencesPartnersManage,
        TenantPermissionKeys.ReferencesProductsManage,
        TenantPermissionKeys.ReferencesWarehousesManage,
        TenantPermissionKeys.ReferencesPricingManage
    ];

    public IReadOnlyList<ReferenceCard> Cards { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await userPermissionService.HasAnyPermissionAsync(User, RequiredPermissions, HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var permissions = await userPermissionService.GetCurrentPermissionKeysAsync(HttpContext.RequestAborted);
        List<ReferenceCard> cards = [];

        if (permissions.Contains(TenantPermissionKeys.ReferencesPartnersManage, StringComparer.OrdinalIgnoreCase))
        {
            cards.Add(new("Clients", "Tiers clients, prospects et conditions de paiement.", "/Partners/Index?scope=customers"));
            cards.Add(new("Fournisseurs", "Tiers fournisseurs et suivi des achats.", "/Partners/Index?scope=suppliers"));
        }

        if (permissions.Contains(TenantPermissionKeys.ReferencesProductsManage, StringComparer.OrdinalIgnoreCase))
        {
            cards.Add(new("Articles", "Catalogue produits et services.", "/Products/Index"));
        }

        if (permissions.Contains(TenantPermissionKeys.ReferencesWarehousesManage, StringComparer.OrdinalIgnoreCase))
        {
            cards.Add(new("Depots", "Sites et depots de stockage.", "/Warehouses/Index"));
        }

        if (permissions.Contains(TenantPermissionKeys.ReferencesPricingManage, StringComparer.OrdinalIgnoreCase))
        {
            cards.Add(new("Conditions de paiement", "Echeances et delais standards.", "/PaymentTerms/Index"));
            cards.Add(new("Taxes", "Codes et taux applicables.", "/TaxCodes/Index"));
            cards.Add(new("Familles d'articles", "Classification du catalogue.", "/ProductCategories/Index"));
            cards.Add(new("Listes de prix", "Tarifs standards et lignes article.", "/PriceLists/Index"));
        }

        Cards = cards;
        return Page();
    }
}

public sealed record ReferenceCard(string Title, string Description, string Href);
