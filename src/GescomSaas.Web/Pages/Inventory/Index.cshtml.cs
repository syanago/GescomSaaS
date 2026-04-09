using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using Microsoft.AspNetCore.Authorization;

namespace GescomSaas.Web.Pages.Inventory;

[Authorize]
public class IndexModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IInventoryService inventoryService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    public InventoryDashboardSnapshot Snapshot { get; private set; } =
        new(0, 0m, 0m, [], []);

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Snapshot = await inventoryService.GetDashboardAsync(tenantId, HttpContext.RequestAborted);
    }
}
