using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SaaSAdmin.Plans;

[Authorize(Roles = "PlatformAdmin")]
public class IndexModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PlatformAdminPageModel(dbContext, userManager)
{
    public IReadOnlyList<PlanListItem> Plans { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Plans = await DbContext.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(x => x.MonthlyPrice)
            .ThenBy(x => x.Label)
            .Select(x => new PlanListItem(x.Id, x.Code, x.Label, x.Edition, x.MonthlyPrice, x.MaxUsers, x.MaxProducts, x.MaxMonthlyDocuments))
            .ToListAsync(HttpContext.RequestAborted);
    }
}

public sealed record PlanListItem(Guid Id, string Code, string Label, GescomSaas.Domain.Enums.TenantEdition Edition, decimal MonthlyPrice, int MaxUsers, int MaxProducts, int MaxMonthlyDocuments);
