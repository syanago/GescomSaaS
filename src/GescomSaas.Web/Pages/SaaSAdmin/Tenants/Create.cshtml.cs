using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Application.Contracts;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SaaSAdmin.Tenants;

[Authorize(Roles = "PlatformAdmin")]
public class CreateModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    ITenantDisplayFormatter displayFormatter) : PlatformAdminPageModel(dbContext, userManager)
{
    [BindProperty]
    public TenantInputModel Input { get; set; } = new();

    public IReadOnlyList<SelectListItem> Plans { get; private set; } = [];

    public async Task OnGetAsync()
    {
        await LoadPlansAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadPlansAsync();
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await DbContext.Tenants.AnyAsync(x => x.Slug == Input.Slug.Trim().ToLowerInvariant(), HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Slug", "Ce slug existe deja.");
            return Page();
        }

        var tenant = new Tenant();
        var subscription = new TenantSubscription();
        Input.ApplyTo(tenant, subscription);
        subscription.Tenant = tenant;

        DbContext.Tenants.Add(tenant);
        DbContext.TenantSubscriptions.Add(subscription);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);

        StatusMessage = $"{tenant.CompanyName} cree.";
        return RedirectToPage("/SaaSAdmin/Tenants/Edit", new { id = tenant.Id });
    }

    private async Task LoadPlansAsync()
    {
        var plans = await DbContext.SubscriptionPlans
            .AsNoTracking()
            .OrderBy(x => x.MonthlyPrice)
            .ThenBy(x => x.Label)
            .ToListAsync(HttpContext.RequestAborted);

        Plans = plans
            .Select(x => new SelectListItem($"{x.Label} ({displayFormatter.Money(x.MonthlyPrice)})", x.Id.ToString()))
            .ToList();
    }
}
