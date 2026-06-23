using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.Products;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesProductsManage];

    public IReadOnlyList<ProductListItem> Products { get; private set; } = [];
    public QuotaUsageItem? ProductQuota { get; private set; }

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(tenantId, cancellationToken: HttpContext.RequestAborted);
        ProductQuota = quotas.FirstOrDefault(x => x.Label == "Articles");

        Products = await DbContext.Products
            .AsNoTracking()
            .Include(x => x.ProductCategory)
            .Include(x => x.TaxCode)
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Sku)
            .Select(x => new ProductListItem(
                x.Id,
                x.Sku,
                x.Label,
                x.ProductType,
                x.UnitOfMeasure,
                x.ProductCategory != null ? x.ProductCategory.Label : "-",
                x.TaxCode != null ? x.TaxCode.Code : "-",
                x.PurchasePrice,
                x.SalesPrice,
                x.TrackStock,
                x.StockValuationMethod,
                x.StockIdentityTrackingMode,
                x.IsActive))
            .ToListAsync(HttpContext.RequestAborted);
    }
}

public sealed record ProductListItem(
    Guid Id,
    string Sku,
    string Label,
    ProductType ProductType,
    string UnitOfMeasure,
    string CategoryLabel,
    string TaxCode,
    decimal PurchasePrice,
    decimal SalesPrice,
    bool TrackStock,
    StockValuationMethod StockValuationMethod,
    StockIdentityTrackingMode StockIdentityTrackingMode,
    bool IsActive);
