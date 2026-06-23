using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.ProductCategories;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    INumberingService numberingService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPricingManage];

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
        var rule = await numberingService.GetReferenceRuleAsync(tenantId, ReferenceNumberingScope.ProductCategory, HttpContext.RequestAborted);
        Input.Code = rule.Preview;
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
        try
        {
            Input.Code = await numberingService.ResolveReferenceCodeAsync(tenantId, ReferenceNumberingScope.ProductCategory, Input.Code, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("Input.Code", exception.Message);
            return Page();
        }

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
