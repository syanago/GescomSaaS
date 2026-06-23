using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Web.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PriceLists;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService) : CommercialPermissionPageModel(dbContext, currentTenantAccessor, userPermissionService)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.ReferencesPricingManage];

    public IReadOnlyList<PriceListListItem> PriceLists { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();

        PriceLists = await DbContext.PriceLists
            .AsNoTracking()
            .Include(x => x.Lines)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Code)
            .Select(x => new PriceListListItem(x.Id, x.Code, x.Label, x.CurrencyCode, x.IsDefault, x.Lines.Count))
            .ToListAsync(HttpContext.RequestAborted);
    }
}

public sealed record PriceListListItem(Guid Id, string Code, string Label, string CurrencyCode, bool IsDefault, int LineCount);
