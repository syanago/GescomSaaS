using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Warehouses;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    INumberingService numberingService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesWarehousesManage];

    [BindProperty]
    public WarehouseInputModel Input { get; set; } = new();

    public QuotaUsageItem? WarehouseQuota { get; private set; }

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var rule = await numberingService.GetReferenceRuleAsync(tenantId, ReferenceNumberingScope.Warehouse, HttpContext.RequestAborted);
        Input.Code = rule.Preview;
        await LoadQuotasAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadQuotasAsync();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        try
        {
            Input.Code = await numberingService.ResolveReferenceCodeAsync(tenantId, ReferenceNumberingScope.Warehouse, Input.Code, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("Input.Code", exception.Message);
            return Page();
        }

        if (await DbContext.Warehouses.AnyAsync(x => x.TenantId == tenantId && x.Code == Input.Code.Trim().ToUpperInvariant(), HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code depot existe deja.");
            return Page();
        }

        try
        {
            await tenantQuotaEnforcementService.EnsureCanCreateWarehouseAsync(tenantId, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return Page();
        }

        if (Input.IsDefault)
        {
            var existingDefaults = await DbContext.Warehouses
                .Where(x => x.TenantId == tenantId && x.IsDefault)
                .ToListAsync(HttpContext.RequestAborted);

            foreach (var item in existingDefaults)
            {
                item.IsDefault = false;
            }
        }

        var warehouse = new Warehouse
        {
            TenantId = tenantId
        };
        Input.ApplyTo(warehouse);

        DbContext.Warehouses.Add(warehouse);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{warehouse.Code} cree.";
        return RedirectToPage("/Warehouses/Index");
    }

    private async Task LoadQuotasAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        WarehouseQuota = quotas.FirstOrDefault(x => x.Label == "Depots");
    }
}
