using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Partners;

[Authorize]
public class DeleteModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public string Scope { get; set; } = PartnerScope.Customers;

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public BusinessPartner? Partner { get; private set; }
    public string? BlockingReason { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Scope = PartnerScope.Normalize(Scope);
        return await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        Scope = PartnerScope.Normalize(Scope);
        var loadResult = await LoadAsync();
        if (loadResult is NotFoundResult)
        {
            return loadResult;
        }

        if (!string.IsNullOrWhiteSpace(BlockingReason))
        {
            return Page();
        }

        DbContext.BusinessPartners.Remove(Partner!);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{Partner!.Code} supprime.";
        return RedirectToPage("/Partners/Index", new { scope = Scope });
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Partner = await DbContext.BusinessPartners
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (Partner is null)
        {
            return NotFound();
        }

        var hasDocuments = await DbContext.CommercialDocuments
            .AnyAsync(x => x.PartnerId == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        BlockingReason = hasDocuments
            ? "Ce tiers est deja utilise dans des documents commerciaux. Desactivez la fiche au lieu de la supprimer."
            : null;

        return Page();
    }
}
