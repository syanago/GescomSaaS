using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.TaxCodes;

[Authorize]
public class DeleteModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPricingManage];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public TaxCode? TaxCode { get; private set; }
    public string? BlockingReason { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await LoadAsync();
        if (result is NotFoundResult) return result;
        if (!string.IsNullOrWhiteSpace(BlockingReason)) return Page();

        DbContext.TaxCodes.Remove(TaxCode!);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{TaxCode!.Code} supprime.";
        return RedirectToPage("/TaxCodes/Index");
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        TaxCode = await DbContext.TaxCodes.FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (TaxCode is null) return NotFound();

        var isUsed = await DbContext.Products.AnyAsync(x => x.TaxCodeId == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        BlockingReason = isUsed ? "Ce code taxe est deja rattache a des articles." : null;
        return Page();
    }
}
