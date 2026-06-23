using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PaymentTerms;

[Authorize]
public class DeleteModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPricingManage];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public PaymentTerm? PaymentTerm { get; private set; }
    public string? BlockingReason { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await LoadAsync();
        if (result is NotFoundResult) return result;
        if (!string.IsNullOrWhiteSpace(BlockingReason)) return Page();

        DbContext.PaymentTerms.Remove(PaymentTerm!);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{PaymentTerm!.Code} supprime.";
        return RedirectToPage("/PaymentTerms/Index");
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        PaymentTerm = await DbContext.PaymentTerms.FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (PaymentTerm is null) return NotFound();

        var isUsed = await DbContext.BusinessPartners.AnyAsync(x => x.PaymentTermId == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        BlockingReason = isUsed ? "Cette condition est deja affectee a des tiers." : null;
        return Page();
    }
}
