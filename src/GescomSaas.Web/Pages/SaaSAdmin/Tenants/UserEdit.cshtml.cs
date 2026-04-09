using System.ComponentModel.DataAnnotations;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GescomSaas.Web.Pages.SaaSAdmin.Tenants;

[Authorize(Roles = "PlatformAdmin")]
public class UserEditModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPlatformUserAdministrationService platformUserAdministrationService) : PlatformAdminPageModel(dbContext, userManager)
{
    [BindProperty(SupportsGet = true)]
    public Guid TenantId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string UserId { get; set; } = string.Empty;

    [BindProperty]
    public TenantUserEditInput Input { get; set; } = new();

    public string TenantName { get; private set; } = string.Empty;
    public string UserEmail { get; private set; } = string.Empty;
    public IReadOnlyList<string> AssignableRoles { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.SelectedRoles.Count == 0)
        {
            ModelState.AddModelError("Input.SelectedRoles", "Selectionnez au moins un role.");
        }

        if (!ModelState.IsValid)
        {
            return await LoadAsync();
        }

        try
        {
            await platformUserAdministrationService.UpdateUserAsync(
                TenantId,
                UserId,
                new TenantUserUpdateRequest(Input.FirstName, Input.LastName, Input.SelectedRoles),
                HttpContext.RequestAborted);

            StatusMessage = "Utilisateur mis a jour.";
            return RedirectToPage(new { tenantId = TenantId, userId = UserId });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await LoadAsync();
        }
    }

    public async Task<IActionResult> OnPostDetachAsync()
    {
        try
        {
            await platformUserAdministrationService.DetachUserAsync(TenantId, UserId, HttpContext.RequestAborted);
            StatusMessage = "Utilisateur detache du tenant.";
            return RedirectToPage("/SaaSAdmin/Tenants/Users", new { id = TenantId });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await LoadAsync();
        }
    }

    private async Task<IActionResult> LoadAsync()
    {
        try
        {
            var snapshot = await platformUserAdministrationService.GetTenantSnapshotAsync(TenantId, HttpContext.RequestAborted);
            var user = snapshot.Users.FirstOrDefault(x => x.UserId == UserId);
            if (user is null)
            {
                return NotFound();
            }

            TenantName = snapshot.TenantName;
            UserEmail = user.Email;
            AssignableRoles = snapshot.AssignableRoles;

            if (!Request.HasFormContentType)
            {
                var currentUser = await UserManager.FindByIdAsync(UserId);
                Input = new TenantUserEditInput
                {
                    FirstName = currentUser?.FirstName ?? string.Empty,
                    LastName = currentUser?.LastName ?? string.Empty,
                    SelectedRoles = user.Roles.ToList()
                };
            }

            return Page();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}

public sealed class TenantUserEditInput
{
    [Required]
    [StringLength(80)]
    [Display(Name = "Prenom")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    public List<string> SelectedRoles { get; set; } = [];
}
