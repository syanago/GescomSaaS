using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Warehouses;

[Authorize]
public class DeleteModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesWarehousesManage];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Warehouse? Warehouse { get; private set; }
    public string? BlockingReason { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await LoadAsync();
        if (result is NotFoundResult)
        {
            return result;
        }

        if (!string.IsNullOrWhiteSpace(BlockingReason))
        {
            return Page();
        }

        DbContext.Warehouses.Remove(Warehouse!);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{Warehouse!.Code} supprime.";
        return RedirectToPage("/Warehouses/Index");
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Warehouse = await DbContext.Warehouses
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (Warehouse is null)
        {
            return NotFound();
        }

        if (Warehouse.IsDefault)
        {
            BlockingReason = "Le depot par defaut ne peut pas etre supprime tant qu'un autre depot n'a pas pris le relais.";
            return Page();
        }

        var isReferenced = await DbContext.CommercialDocuments.AnyAsync(x => x.WarehouseId == Id && x.TenantId == tenantId, HttpContext.RequestAborted)
            || await DbContext.StockMovements.AnyAsync(x => x.WarehouseId == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        BlockingReason = isReferenced
            ? "Ce depot est deja utilise dans des documents ou mouvements de stock."
            : null;

        return Page();
    }
}
