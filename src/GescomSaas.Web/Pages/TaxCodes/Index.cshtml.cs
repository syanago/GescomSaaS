using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.TaxCodes;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    public IReadOnlyList<TaxCodeListItem> TaxCodes { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        TaxCodes = await DbContext.TaxCodes
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new TaxCodeListItem(x.Id, x.Code, x.Label, x.Rate))
            .ToListAsync(HttpContext.RequestAborted);
    }
}

public sealed record TaxCodeListItem(Guid Id, string Code, string Label, decimal Rate);
