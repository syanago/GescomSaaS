using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.ProductCategories;

[Authorize]
public class EditModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public ProductCategoryInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.ProductCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (entity is null) return NotFound();
        Input = ProductCategoryInputModel.FromEntity(entity);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.ProductCategories.FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (entity is null) return NotFound();
        if (await DbContext.ProductCategories.AnyAsync(x => x.TenantId == tenantId && x.Code == Input.Code.Trim().ToUpperInvariant() && x.Id != Id, HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            return Page();
        }

        Input.ApplyTo(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{entity.Code} mis a jour.";
        return RedirectToPage("/ProductCategories/Index");
    }
}
