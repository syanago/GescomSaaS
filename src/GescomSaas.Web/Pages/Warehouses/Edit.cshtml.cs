using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Warehouses;

[Authorize]
public class EditModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public WarehouseInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        Input = WarehouseInputModel.FromEntity(entity);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.Warehouses
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        if (await DbContext.Warehouses.AnyAsync(x => x.TenantId == tenantId && x.Code == Input.Code.Trim().ToUpperInvariant() && x.Id != Id, HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code depot existe deja.");
            return Page();
        }

        if (Input.IsDefault)
        {
            var defaults = await DbContext.Warehouses
                .Where(x => x.TenantId == tenantId && x.IsDefault && x.Id != Id)
                .ToListAsync(HttpContext.RequestAborted);

            foreach (var item in defaults)
            {
                item.IsDefault = false;
            }
        }

        Input.ApplyTo(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{entity.Code} mis a jour.";
        return RedirectToPage("/Warehouses/Index");
    }
}
