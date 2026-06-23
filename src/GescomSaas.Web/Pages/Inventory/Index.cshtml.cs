using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;

namespace GescomSaas.Web.Pages.Inventory;

[Authorize]
public class IndexModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    IInventoryService inventoryService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.InventoryManage];

    public InventoryDashboardSnapshot Snapshot { get; private set; } =
        new(0, 0m, 0m, [], []);

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Snapshot = await inventoryService.GetDashboardAsync(tenantId, HttpContext.RequestAborted);
    }
}
