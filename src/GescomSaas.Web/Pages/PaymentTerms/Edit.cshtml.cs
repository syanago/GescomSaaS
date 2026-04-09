using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PaymentTerms;

[Authorize]
public class EditModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public PaymentTermInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.PaymentTerms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (entity is null) return NotFound();
        Input = PaymentTermInputModel.FromEntity(entity);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        var tenantId = await GetTenantIdAsync();
        var entity = await DbContext.PaymentTerms.FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (entity is null) return NotFound();
        if (await DbContext.PaymentTerms.AnyAsync(x => x.TenantId == tenantId && x.Code == Input.Code.Trim().ToUpperInvariant() && x.Id != Id, HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            return Page();
        }

        Input.ApplyTo(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{entity.Code} mis a jour.";
        return RedirectToPage("/PaymentTerms/Index");
    }
}
