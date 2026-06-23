using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Products;

[Authorize]
public class DeleteModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesProductsManage];

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public Product? Product { get; private set; }
    public string? BlockingReason { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        var result = await LoadAsync();
        if (result is NotFoundResult)
        {
            return result;
        }

        if (!string.IsNullOrWhiteSpace(BlockingReason))
        {
            return Page();
        }

        DbContext.Products.Remove(Product!);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{Product!.Sku} supprime.";
        return RedirectToPage("/Products/Index");
    }

    private async Task<IActionResult> LoadAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Product = await DbContext.Products
            .FirstOrDefaultAsync(x => x.Id == Id && x.TenantId == tenantId, HttpContext.RequestAborted);

        if (Product is null)
        {
            return NotFound();
        }

        var isReferenced = await DbContext.CommercialDocumentLines.AnyAsync(x => x.ProductId == Id, HttpContext.RequestAborted)
            || await DbContext.StockMovements.AnyAsync(x => x.ProductId == Id && x.TenantId == tenantId, HttpContext.RequestAborted)
            || await DbContext.PriceListLines.AnyAsync(x => x.ProductId == Id, HttpContext.RequestAborted);

        BlockingReason = isReferenced
            ? "Cet article est deja lie a des documents, tarifs ou mouvements de stock. Desactivez-le au lieu de le supprimer."
            : null;

        return Page();
    }
}
