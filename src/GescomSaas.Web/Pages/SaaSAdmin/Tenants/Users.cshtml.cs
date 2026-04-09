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
public class UsersModel(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPlatformUserAdministrationService platformUserAdministrationService,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService) : PlatformAdminPageModel(dbContext, userManager)
{
    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    [BindProperty]
    public InvitationFormInput Invitation { get; set; } = new();

    [BindProperty]
    public AttachUserFormInput Attachment { get; set; } = new();

    public TenantUserManagementSnapshot? Snapshot { get; private set; }
    public QuotaUsageItem? UserQuota { get; private set; }

    public IReadOnlyList<string> AssignableRoles => Snapshot?.AssignableRoles ?? [];
    public bool IsUserQuotaSaturated => UserQuota is not null && UserQuota.Used >= UserQuota.Limit;
    public int RemainingUserSlots => UserQuota is null ? 0 : UserQuota.Limit - UserQuota.Used;

    public async Task<IActionResult> OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostInviteAsync()
    {
        if (Invitation.SelectedRoles.Count == 0)
        {
            ModelState.AddModelError("Invitation.SelectedRoles", "Selectionnez au moins un role.");
        }

        if (!ModelState.IsValid)
        {
            return await LoadAsync();
        }

        try
        {
            var inviteUrl = await platformUserAdministrationService.CreateInvitationAsync(
                Id,
                new UserInvitationRequest(
                    Invitation.Email,
                    Invitation.FirstName,
                    Invitation.LastName,
                    Invitation.SelectedRoles,
                    Invitation.Notes),
                HttpContext.RequestAborted);

            StatusMessage = $"Invitation creee. Lien genere: {inviteUrl}";
            return RedirectToPage(new { id = Id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await LoadAsync();
        }
    }

    public async Task<IActionResult> OnPostAttachAsync()
    {
        if (Attachment.SelectedRoles.Count == 0)
        {
            ModelState.AddModelError("Attachment.SelectedRoles", "Selectionnez au moins un role.");
        }

        if (!ModelState.IsValid)
        {
            return await LoadAsync();
        }

        try
        {
            await platformUserAdministrationService.AttachExistingUserAsync(
                Id,
                Attachment.UserId,
                Attachment.SelectedRoles,
                HttpContext.RequestAborted);

            StatusMessage = "Utilisateur rattache au tenant.";
            return RedirectToPage(new { id = Id });
        }
        catch (InvalidOperationException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return await LoadAsync();
        }
    }

    public async Task<IActionResult> OnPostCancelInvitationAsync(Guid invitationId)
    {
        try
        {
            await platformUserAdministrationService.CancelInvitationAsync(Id, invitationId, HttpContext.RequestAborted);
            StatusMessage = "Invitation annulee.";
            return RedirectToPage(new { id = Id });
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
            Snapshot = await platformUserAdministrationService.GetTenantSnapshotAsync(Id, HttpContext.RequestAborted);
            var quotas = await tenantQuotaEnforcementService.GetQuotaUsageAsync(Id, cancellationToken: HttpContext.RequestAborted);
            UserQuota = quotas.FirstOrDefault(x => x.Label == "Utilisateurs");
            return Page();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}

public sealed class InvitationFormInput
{
    [Required]
    [EmailAddress]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    [Display(Name = "Prenom")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    [Display(Name = "Nom")]
    public string LastName { get; set; } = string.Empty;

    [StringLength(600)]
    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public List<string> SelectedRoles { get; set; } = [];
}

public sealed class AttachUserFormInput
{
    [Required]
    [Display(Name = "Utilisateur disponible")]
    public string UserId { get; set; } = string.Empty;

    public List<string> SelectedRoles { get; set; } = [];
}
