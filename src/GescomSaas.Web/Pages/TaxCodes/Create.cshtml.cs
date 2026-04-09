using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.TaxCodes;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty]
    public TaxCodeInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var tenantId = await GetTenantIdAsync();
        if (await DbContext.TaxCodes.AnyAsync(x => x.TenantId == tenantId && x.Code == Input.Code.Trim().ToUpperInvariant(), HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            return Page();
        }

        var entity = new TaxCode { TenantId = tenantId };
        Input.ApplyTo(entity);
        DbContext.TaxCodes.Add(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{entity.Code} cree.";
        return RedirectToPage("/TaxCodes/Index");
    }
}
