using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Inventory;

[Authorize]
public class LotsModel(
    GescomSaas.Infrastructure.Persistence.ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IInventoryService inventoryService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid? ProductId { get; set; }

    [BindProperty(SupportsGet = true)]
    public Guid? WarehouseId { get; set; }

    public IReadOnlyList<SelectListItem> Products { get; private set; } = [];
    public IReadOnlyList<SelectListItem> Warehouses { get; private set; } = [];
    public IReadOnlyList<InventoryLotItem> Lots { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        await LoadLookupsAsync(tenantId);
        Lots = await inventoryService.GetLotStocksAsync(tenantId, ProductId, WarehouseId, HttpContext.RequestAborted);
    }

    private async Task LoadLookupsAsync(Guid tenantId)
    {
        Products = await DbContext.Products
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TrackStock)
            .OrderBy(x => x.Sku)
            .Select(x => new SelectListItem($"{x.Sku} - {x.Label}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);

        Warehouses = await DbContext.Warehouses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new SelectListItem($"{x.Code} - {x.Label}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);
    }
}
