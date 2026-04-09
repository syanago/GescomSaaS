using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Products;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty]
    public ProductInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> Categories { get; private set; } = [];
    public IReadOnlyList<SelectListItem> TaxCodes { get; private set; } = [];
    public QuotaUsageItem? ProductQuota { get; private set; }

    public IReadOnlyList<SelectListItem> ProductTypes { get; } =
    [
        new("Article stocke", ProductType.StockItem.ToString()),
        new("Service", ProductType.Service.ToString()),
        new("Nomenclature", ProductType.Bundle.ToString())
    ];

    public async Task OnGetAsync()
    {
        await LoadLookupsAsync();
        await LoadQuotasAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();
        await LoadQuotasAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        if (await DbContext.Products.AnyAsync(x => x.TenantId == tenantId && x.Sku == Input.Sku.Trim(), HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Sku", "Cette reference existe deja.");
            return Page();
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanCreateProductAsync(tenantId, Input.IsActive, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }

        var product = new Product
        {
            TenantId = tenantId
        };

        Input.ApplyTo(product);

        DbContext.Products.Add(product);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{product.Sku} cree.";
        return RedirectToPage("/Products/Index");
    }

    private async Task LoadLookupsAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Categories = await DbContext.ProductCategories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new SelectListItem($"{x.Code} - {x.Label}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);

        TaxCodes = await DbContext.TaxCodes
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new SelectListItem($"{x.Code} - {x.Label}", x.Id.ToString()))
            .ToListAsync(HttpContext.RequestAborted);
    }

    private async Task LoadQuotasAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        ProductQuota = quotas.FirstOrDefault(x => x.Label == "Articles");
    }
}
