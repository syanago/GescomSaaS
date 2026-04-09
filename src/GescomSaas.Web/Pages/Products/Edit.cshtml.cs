using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Products;

[Authorize]
public class EditModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public ProductInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> Categories { get; private set; } = [];
    public IReadOnlyList<SelectListItem> TaxCodes { get; private set; } = [];
    public QuotaUsageItem? ProductQuota { get; private set; }
    public bool OriginalIsActive { get; private set; }

    public IReadOnlyList<SelectListItem> ProductTypes { get; } =
    [
        new("Article stocke", ProductType.StockItem.ToString()),
        new("Service", ProductType.Service.ToString()),
        new("Nomenclature", ProductType.Bundle.ToString())
    ];

    public async Task<IActionResult> OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        Input = ProductInputModel.FromEntity(entity);
        OriginalIsActive = entity.IsActive;
        await LoadLookupsAsync();
        await LoadQuotasAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadLookupsAsync();
        await LoadOriginalStateAsync();
        await LoadQuotasAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.Products
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (entity is null)
        {
            return NotFound();
        }

        if (await DbContext.Products.AnyAsync(x => x.TenantId == tenantId && x.Sku == Input.Sku.Trim() && x.Id != Id, HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Sku", "Cette reference existe deja.");
            return Page();
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanUpdateProductAsync(tenantId, Id, Input.IsActive, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }

        Input.ApplyTo(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{entity.Sku} mis a jour.";
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

    private async Task LoadOriginalStateAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.Products
            .AsNoTracking()
            .Where(x => x.Id == Id && x.TenantId == tenantId)
            .Select(x => new
            {
                x.IsActive
            })
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (entity is not null)
        {
            OriginalIsActive = entity.IsActive;
        }
    }

    private async Task LoadQuotasAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        ProductQuota = quotas.FirstOrDefault(x => x.Label == "Articles");
    }
}
