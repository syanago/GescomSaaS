using GescomSaas.Application.Contracts;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PriceLists;

[Authorize]
public class CreateModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    INumberingService numberingService) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    [BindProperty]
    public PriceListInputModel Input { get; set; } = new();

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var rule = await numberingService.GetReferenceRuleAsync(tenantId, ReferenceNumberingScope.PriceList, HttpContext.RequestAborted);
        Input.Code = rule.Preview;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        try
        {
            Input.Code = await numberingService.ResolveReferenceCodeAsync(tenantId, ReferenceNumberingScope.PriceList, Input.Code, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError("Input.Code", exception.Message);
            return Page();
        }

        var code = Input.Code.Trim().ToUpperInvariant();
        if (await DbContext.PriceLists.AnyAsync(x => x.TenantId == tenantId && x.Code == code, HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            return Page();
        }

        if (Input.IsDefault)
        {
            await ResetDefaultAsync(tenantId);
        }

        var entity = new PriceList { TenantId = tenantId };
        Input.ApplyTo(entity);

        DbContext.PriceLists.Add(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{entity.Code} cree. Vous pouvez maintenant ajouter des lignes tarifaires.";
        return RedirectToPage("/PriceLists/Edit", new { id = entity.Id });
    }

    private async Task ResetDefaultAsync(Guid tenantId)
    {
        var defaults = await DbContext.PriceLists.Where(x => x.TenantId == tenantId && x.IsDefault).ToListAsync(HttpContext.RequestAborted);
        foreach (var item in defaults)
        {
            item.IsDefault = false;
        }
    }
}
