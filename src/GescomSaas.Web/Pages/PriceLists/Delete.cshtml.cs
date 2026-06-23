using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PriceLists;

[Authorize]
public class DeleteModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPricingManage];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public PriceList? PriceList { get; private set; }
    public int LineCount { get; private set; }
    public string? BlockingReason { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await LoadAsync();
        if (result is NotFoundResult) return result;
        if (!string.IsNullOrWhiteSpace(BlockingReason)) return Page();

        DbContext.PriceLists.Remove(PriceList!);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{PriceList!.Code} supprime.";
        return RedirectToPage("/PriceLists/Index");
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        PriceList = await DbContext.PriceLists.FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (PriceList is null) return NotFound();

        LineCount = await DbContext.PriceListLines.CountAsync(x => x.PriceListId == Id, HttpContext.RequestAborted);
        BlockingReason = PriceList.IsDefault ? "La liste de prix par defaut ne peut pas etre supprimee tant qu'une autre n'a pas pris le relais." : null;
        return Page();
    }
}
