using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Web.Pages.SaaSAdmin.Plans;

[Authorize(Roles = "PlatformAdmin")]
public class EditModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PlatformAdminPageModel(dbContext, userManager)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public PlanInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var entity = await DbContext.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == Id, HttpContext.RequestAborted);
        if (entity is null) return NotFound();
        Input = PlanInputModel.FromEntity(entity);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var entity = await DbContext.SubscriptionPlans.FirstOrDefaultAsync(x => x.Id == Id, HttpContext.RequestAborted);
        if (entity is null) return NotFound();

        if (await DbContext.SubscriptionPlans.AnyAsync(x => x.Code == Input.Code.Trim().ToUpperInvariant() && x.Id != Id, HttpContext.RequestAborted))
        {
            ModelState.AddModelError("Input.Code", "Ce code existe deja.");
            return Page();
        }

        Input.ApplyTo(entity);
        await DbContext.SaveChangesAsync(HttpContext.RequestAborted);
        StatusMessage = $"{entity.Code} mis a jour.";
        return RedirectToPage("/SaaSAdmin/Plans/Index");
    }
}
