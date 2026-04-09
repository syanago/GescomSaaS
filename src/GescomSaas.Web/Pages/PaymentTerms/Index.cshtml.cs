using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.PaymentTerms;

[Authorize]
public class IndexModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : CommercialPageModel(dbContext, currentTenantAccessor)
{
    public IReadOnlyList<PaymentTermListItem> PaymentTerms { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var tenantId = await GetTenantIdAsync();
        PaymentTerms = await DbContext.PaymentTerms
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Code)
            .Select(x => new PaymentTermListItem(x.Id, x.Code, x.Label, x.DueInDays))
            .ToListAsync(HttpContext.RequestAborted);
    }
}

public sealed record PaymentTermListItem(Guid Id, string Code, string Label, int DueInDays);
