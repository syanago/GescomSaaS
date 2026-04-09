using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GescomSaas.Web.Pages.SaaSAdmin;

[Authorize(Roles = "PlatformAdmin")]
public class IndexModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPlatformAdministrationService platformAdministrationService) : PlatformAdminPageModel(dbContext, userManager)
{
    public PlatformAdminDashboardSnapshot Dashboard { get; private set; } = PlatformAdminDashboardSnapshot.Empty;

    public async Task OnGetAsync()
    {
        Dashboard = await platformAdministrationService.GetDashboardAsync(HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostAcknowledgeNotificationAsync(Guid notificationId)
    {
        try
        {
            await platformAdministrationService.AcknowledgeQuotaNotificationAsync(notificationId, HttpContext.RequestAborted);
            StatusMessage = "Notification acquittee.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }

        return RedirectToPage();
    }
}
