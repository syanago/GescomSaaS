using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages;

public abstract class CommercialPageModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : PageModel
{
    protected ApplicationDbContext DbContext { get; } = dbContext;
    protected ICurrentTenantAccessor CurrentTenantAccessor { get; } = currentTenantAccessor;

    [TempData]
    public string? StatusMessage { get; set; }

    protected async Task<Guid> GetTenantIdAsync()
    {
        var currentTenantId = CurrentTenantAccessor.GetTenantId();
        if (currentTenantId.HasValue)
        {
            return currentTenantId.Value;
        }

        var fallbackTenantId = await DbContext.Tenants
            .AsNoTracking()
            .OrderBy(x => x.CompanyName)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(HttpContext.RequestAborted);

        if (!fallbackTenantId.HasValue)
        {
            throw new InvalidOperationException("Aucun tenant n'est disponible dans la base.");
        }

        return fallbackTenantId.Value;
    }
}
