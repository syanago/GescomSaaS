using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages;

public class IndexModel(ICommercialDashboardService dashboardService) : PageModel
{
    public DashboardSnapshot Dashboard { get; private set; } = DashboardSnapshot.Empty([]);

    public async Task OnGetAsync()
    {
        Dashboard = await dashboardService.GetDashboardAsync(HttpContext.RequestAborted);
    }
}
