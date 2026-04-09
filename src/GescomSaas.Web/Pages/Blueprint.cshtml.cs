using GescomSaas.Application.Catalog;
using GescomSaas.Application.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages;

public class BlueprintModel : PageModel
{
    public IReadOnlyList<FeatureModule> Modules => CommercialFeatureCatalog.Modules;
    public IReadOnlyList<string> Roadmap => CommercialFeatureCatalog.Roadmap;

    public IReadOnlyList<string> TechnicalDecisions { get; } =
    [
        "ASP.NET Core Razor Pages pour une interface C# end-to-end dans Visual Studio.",
        "Architecture en couches Domain, Application, Infrastructure et Web.",
        "SQL Server via Entity Framework Core et modele multi-tenant.",
        "ASP.NET Core Identity pour l'authentification et la gestion des comptes SaaS.",
        "Seed initial pour disposer d'un tenant, de documents et d'un compte de demonstration."
    ];

    public void OnGet()
    {
    }
}
