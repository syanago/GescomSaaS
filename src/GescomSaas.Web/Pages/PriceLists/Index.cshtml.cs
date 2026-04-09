using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PriceLists;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
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
