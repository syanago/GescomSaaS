using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SaaSAdmin.Plans;

[Authorize(Roles = "PlatformAdmin")]
public class CreateModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PlatformAdminPageModel(dbContext, userManager)
{
    [BindProperty]
    public PlanInputModel Input { get; set; } = new();

    public IActionResult OnGet() => Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await DbContext.SubscriptionPlans.AnyAsync(x => x.Code == Input.Code.Trim().ToUpperInvariant(), HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            return Page();
        }

        var entity = new SubscriptionPlan();
        Input.ApplyTo(entity);
        DbContext.SubscriptionPlans.Add(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{entity.Code} cree.";
        return RedirectToPage("/SaaSAdmin/Plans/Index");
    }
}
