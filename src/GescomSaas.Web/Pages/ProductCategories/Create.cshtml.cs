using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.ProductCategories;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty]
    public ProductCategoryInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> StockValuationMethodOptions { get; } =
    [
        new("CMUP", StockValuationMethod.Cmup.ToString()),
        new("FIFO", StockValuationMethod.Fifo.ToString()),
        new("Dernier prix d'achat", StockValuationMethod.LastPurchaseCost.ToString())
    ];

    public IReadOnlyList<SelectListItem> StockIdentityTrackingModeOptions { get; } =
    [
        new("Aucun", StockIdentityTrackingMode.None.ToString()),
        new("Par lot", StockIdentityTrackingMode.Lot.ToString()),
        new("Par numero de serie", StockIdentityTrackingMode.SerialNumber.ToString())
    ];

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var tenant = await DbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, HttpContext.RequestAborted);

        if (tenant is not null)
        {
            Input.StockValuationMethod = tenant.DefaultStockValuationMethod;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var tenantId = await GetTenantIdAsync();
        if (await DbContext.ProductCategories.AnyAsync(x => x.TenantId == tenantId && x.Code == Input.Code.Trim().ToUpperInvariant(), HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            return Page();
        }

        var entity = new ProductCategory { TenantId = tenantId };
        Input.ApplyTo(entity);
        DbContext.ProductCategories.Add(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{entity.Code} cree.";
        return RedirectToPage("/ProductCategories/Index");
    }
}
