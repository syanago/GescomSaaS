using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages;

// Securite : le tableau de bord d'accueil exige une authentification.
// Consequence : au demarrage, l'acces a "/" redirige vers la page de connexion.
[Authorize]
public class IndexModel(ICommercialDashboardService dashboardService) : PageModel
{
    public DashboardSnapshot Dashboard { get; private set; } = DashboardSnapshot.Empty([]);
    public string? LoadErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            Dashboard = await dashboardService.GetDashboardAsync(HttpContext.RequestAborted);
        }
        catch (SqlException)
        {
            LoadErrorMessage = "LigCom n'arrive pas a joindre SQL Server pour charger le cockpit. Le site reste accessible, mais la base doit etre reconnectee.";
            Dashboard = DashboardSnapshot.Empty([]);
        }
    }
}
