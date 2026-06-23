using System.ComponentModel.DataAnnotations;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Configuration;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace GescomSaas.Web.Pages.Settings;

public class AccessProfilesModel(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor,
    IUserPermissionService userPermissionService,
    ITenantAccessProfileService tenantAccessProfileService,
    IOptions<LigComRuntimeOptions> runtimeOptions) : SettingsPageModel(dbContext, currentTenantAccessor, userPermissionService, runtimeOptions)
{
    protected override IReadOnlyCollection<string> RequiredPermissionKeys => [TenantPermissionKeys.SettingsAccessProfilesManage];

    [BindProperty(SupportsGet = true)]
    public Guid? ProfileId { get; set; }

    [BindProperty]
    public AccessProfileInputModel Profile { get; set; } = new();

    public TenantAccessProfileSnapshot? Snapshot { get; private set; }
    public IReadOnlyList<TenantAccessProfileItem> DefaultProfiles => Snapshot?.Profiles.Where(x => x.IsDefault).ToArray() ?? [];
    public IReadOnlyList<TenantAccessProfileItem> AssignableProfiles => Snapshot?.Profiles.Where(x => !x.IsDefault).ToArray() ?? [];
    public IReadOnlyDictionary<string, string> PermissionLabels { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public async Task OnGetAsync() => await LoadAsync(ProfileId);

    public async Task<IActionResult> OnPostSaveProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(Profile.Name))
        {
            ModelState.AddModelError("Profile.Name", "Le nom du profil est obligatoire.");
        }

        if (Profile.SelectedPermissionKeys.Count == 0)
        {
            ModelState.AddModelError("Profile.SelectedPermissionKeys", "Selectionne au moins un droit granulaire.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(Profile.ProfileId);
            return Page();
        }

        var tenantId = await GetTenantIdAsync();
        var savedProfileId = await tenantAccessProfileService.SaveProfileAsync(
            tenantId,
            new TenantAccessProfileUpsertRequest(
                Profile.ProfileId,
                Profile.Name,
                Profile.Description,
                Profile.IsDefault,
                Profile.SelectedPermissionKeys),
            HttpContext.RequestAborted);

        StatusMessage = $"Le profil {Profile.Name.Trim()} a ete enregistre.";
        return RedirectToPage(new { profileId = savedProfileId });
    }

    public IActionResult OnPostEditProfile(Guid profileId) => RedirectToPage(new { profileId });

    public async Task<IActionResult> OnPostDeleteProfileAsync(Guid profileId)
    {
        var tenantId = await GetTenantIdAsync();
        await tenantAccessProfileService.DeleteProfileAsync(tenantId, profileId, HttpContext.RequestAborted);
        StatusMessage = "Le profil a ete supprime.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLoadStandardProfilesAsync()
    {
        var tenantId = await GetTenantIdAsync();
        var affectedProfiles = await tenantAccessProfileService.EnsureStandardProfilesAsync(tenantId, HttpContext.RequestAborted);
        StatusMessage = affectedProfiles == 0
            ? "Les profils standards LigCom etaient deja en place."
            : $"{affectedProfiles} profil(s) standard(s) LigCom ont ete crees ou resynchronises.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveAssignmentsAsync(string userId, List<Guid>? profileIds)
    {
        var tenantId = await GetTenantIdAsync();
        await tenantAccessProfileService.UpdateUserAssignmentsAsync(
            tenantId,
            new TenantAccessUserAssignmentRequest(userId, profileIds ?? []),
            HttpContext.RequestAborted);

        StatusMessage = "Les affectations de profils ont ete mises a jour.";
        return RedirectToPage(new { profileId = ProfileId });
    }

    public IActionResult OnPostCreateProfileAsync() => RedirectToPage();

    private async Task LoadAsync(Guid? selectedProfileId)
    {
        var tenantId = await GetTenantIdAsync();
        Snapshot = await tenantAccessProfileService.GetSnapshotAsync(tenantId, HttpContext.RequestAborted);
        PermissionLabels = Snapshot.PermissionGroups
            .SelectMany(static group => group.Permissions)
            .ToDictionary(x => x.Key, x => x.Label, StringComparer.OrdinalIgnoreCase);

        var selectedProfile = Snapshot.Profiles.FirstOrDefault(x => x.Id == selectedProfileId);
        if (selectedProfile is null)
        {
            Profile = new AccessProfileInputModel();
            ProfileId = null;
            return;
        }

        ProfileId = selectedProfile.Id;
        Profile = new AccessProfileInputModel
        {
            ProfileId = selectedProfile.Id,
            Name = selectedProfile.Name,
            Description = selectedProfile.Description,
            IsDefault = selectedProfile.IsDefault,
            SelectedPermissionKeys = selectedProfile.PermissionKeys.ToList()
        };
    }
}

public sealed class AccessProfileInputModel
{
    public Guid? ProfileId { get; set; }

    [Required]
    [StringLength(120)]
    [Display(Name = "Nom du profil")]
    public string Name { get; set; } = string.Empty;

    [StringLength(600)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Profil par defaut du tenant")]
    public bool IsDefault { get; set; }

    public List<string> SelectedPermissionKeys { get; set; } = [];
}
