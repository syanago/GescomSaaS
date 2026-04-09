using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SaaSAdmin.Plans;

[Authorize(Roles = "PlatformAdmin")]
public class DeleteModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PlatformAdminPageModel(dbContext, userManager)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public GescomSaas.Domain.Entities.SaaS.SubscriptionPlan? Plan { get; private set; }
    public bool IsUsed { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Plan = await DbContext.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id, HttpContext.RequestAborted);
        if (Plan is null) return NotFound();
        IsUsed = await DbContext.TenantSubscriptions.AnyAsync(x => x.SubscriptionPlanId == Id, HttpContext.RequestAborted);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var plan = await DbContext.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == Id, HttpContext.RequestAborted);
        if (plan is null) return NotFound();
        if (await DbContext.TenantSubscriptions.AnyAsync(x => x.SubscriptionPlanId == Id, HttpContext.RequestAborted))
        {
            StatusMessage = "Ce plan est deja utilise.";
            return RedirectToPage("/SaaSAdmin/Plans/Index");
        }

        DbContext.SubscriptionPlans.Remove(plan);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{plan.Code} supprime.";
        return RedirectToPage("/SaaSAdmin/Plans/Index");
    }
}
