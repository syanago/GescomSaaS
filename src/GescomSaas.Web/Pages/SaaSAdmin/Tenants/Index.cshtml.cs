using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GescomSaas.Web.Pages.SaaSAdmin.Tenants;

[Authorize(Roles = "PlatformAdmin")]
public class IndexModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPlatformAdministrationService platformAdministrationService) : PlatformAdminPageModel(dbContext, userManager)
{
    public IReadOnlyList<TenantAdminSummary> Tenants { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Tenants = await platformAdministrationService.GetTenantSummariesAsync(HttpContext.RequestAborted);
    }

    public async Task<IActionResult> OnPostGenerateInvoiceAsync(Guid tenantId)
    {
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var invoice = await platformAdministrationService.GeneratePlatformInvoiceAsync(
                tenantId,
                today,
                today.AddDays(15),
                HttpContext.RequestAborted);

            StatusMessage = $"Facture plateforme {invoice.InvoiceNumber} generee.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }

        return RedirectToPage();
    }
}
