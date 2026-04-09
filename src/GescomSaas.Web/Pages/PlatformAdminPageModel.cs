using GescomSaas.Infrastructure.Persistence;
using GescomSaas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GescomSaas.Web.Pages;

public abstract class PlatformAdminPageModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : PageModel
{
    protected ApplicationDbContext DbContext { get; } = dbContext;
    protected UserManager<ApplicationUser> UserManager { get; } = userManager;

    [TempData]
    public string? StatusMessage { get; set; }
}
