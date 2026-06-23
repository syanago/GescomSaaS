using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Warehouses;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesWarehousesManage];

    public IReadOnlyList<WarehouseListItem> Warehouses { get; private set; } = [];
    public QuotaUsageItem? WarehouseQuota { get; private set; }

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        WarehouseQuota = quotas.FirstOrDefault(x => x.Label == "Depots");

        Warehouses = await DbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Code)
            .Select(x => new WarehouseListItem(x.Id, x.Code, x.Label, x.IsDefault))
            .ToListAsync(HttpContext.RequestAborted);
    }
}

public sealed record WarehouseListItem(Guid Id, string Code, string Label, bool IsDefault);
