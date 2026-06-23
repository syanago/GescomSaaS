using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.ProductCategories;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPricingManage];

    public IReadOnlyList<ProductCategoryListItem> Categories { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        Categories = await DbContext.ProductCategories
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new ProductCategoryListItem(x.Id, x.Code, x.Label, x.StockValuationMethod, x.StockIdentityTrackingMode))
            .ToListAsync(HttpContext.RequestAborted);
    }
}

public sealed record ProductCategoryListItem(Guid Id, string Code, string Label, StockValuationMethod StockValuationMethod, StockIdentityTrackingMode StockIdentityTrackingMode);
