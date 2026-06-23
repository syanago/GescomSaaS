using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages;

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
