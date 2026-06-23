using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.ProductCategories;

[Authorize]
public class DeleteModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPricingManage];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public ProductCategory? Category { get; private set; }
    public string? BlockingReason { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await LoadAsync();
        if (result is NotFoundResult) return result;
        if (!string.IsNullOrWhiteSpace(BlockingReason)) return Page();

        DbContext.ProductCategories.Remove(Category!);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{Category!.Code} supprime.";
        return RedirectToPage("/ProductCategories/Index");
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Category = await DbContext.ProductCategories.FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        if (Category is null) return NotFound();

        var isUsed = await DbContext.Products.AnyAsync(x => x.ProductCategoryId == Id && x.TenantId == tenantId, HttpContext.RequestAborted);
        BlockingReason = isUsed ? "Cette famille est deja rattachee a des articles." : null;
        return Page();
    }
}
