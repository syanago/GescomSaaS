using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages.ReferenceData;

[Authorize]
public class IndexModel : PageModel
{
    public IReadOnlyList<ReferenceCard> Cards { get; } =
    [
        new("Clients", "Tiers clients, prospects et conditions de paiement.", "/Partners/Index?scope=customers"),
        new("Fournisseurs", "Tiers fournisseurs et suivi des achats.", "/Partners/Index?scope=suppliers"),
        new("Articles", "Catalogue produits et services.", "/Products/Index"),
        new("Depots", "Sites et depots de stockage.", "/Warehouses/Index"),
        new("Conditions de paiement", "Echeances et delais standards.", "/PaymentTerms/Index"),
        new("Taxes", "Codes et taux applicables.", "/TaxCodes/Index"),
        new("Familles d'articles", "Classification du catalogue.", "/ProductCategories/Index"),
        new("Listes de prix", "Tarifs standards et lignes article.", "/PriceLists/Index")
    ];
}

public sealed record ReferenceCard(string Title, string Description, string Href);
