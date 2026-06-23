using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class TenantAccessProfileService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager) : ITenantAccessProfileService
{
    private static readonly IReadOnlyList<StandardAccessProfileDefinition> StandardProfiles =
    [
        new(
            "Administrateur tenant",
            "Acces complet au tenant, a ses parametres et a ses traitements.",
            false,
            TenantPermissionCatalog.AllKeys),
        new(
            "Responsable commercial",
            "Pilotage et gestion des ventes, des clients et des articles.",
            false,
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.SalesDocumentsManage,
                TenantPermissionKeys.ReferencesPartnersManage,
                TenantPermissionKeys.ReferencesProductsManage
            ]),
        new(
            "Responsable achats",
            "Gestion des achats, des fournisseurs et des articles.",
            false,
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.PurchasesDocumentsManage,
                TenantPermissionKeys.ReferencesPartnersManage,
                TenantPermissionKeys.ReferencesProductsManage
            ]),
        new(
            "Responsable finance",
            "Reglements, acomptes, relances et situation client.",
            false,
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.FinanceManage,
                TenantPermissionKeys.ReferencesPartnersManage
            ]),
        new(
            "Responsable stock",
            "Pilotage du stock, des depots et des articles.",
            false,
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.InventoryManage,
                TenantPermissionKeys.ReferencesProductsManage,
                TenantPermissionKeys.ReferencesWarehousesManage
            ]),
        new(
            "Gestionnaire referentiels",
            "Mise a jour des tiers, articles, depots et regles tarifaires.",
            false,
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.ReferencesPartnersManage,
                TenantPermissionKeys.ReferencesProductsManage,
                TenantPermissionKeys.ReferencesWarehousesManage,
                TenantPermissionKeys.ReferencesPricingManage
            ]),
        new(
            "Consultation direction",
            "Lecture seule du cockpit et des indicateurs principaux.",
            false,
            [
                TenantPermissionKeys.DashboardView
            ]),
        new(
            "Import Sage",
            "Acces specialise au parametrage et au suivi des imports Sage.",
            false,
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.SettingsSageImportManage
            ]),
        new(
            "Parametrage metier",
            "Acces aux reglages metier courants sans administration des droits.",
            false,
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.SettingsCompanyManage,
                TenantPermissionKeys.SettingsNumberingManage
            ]),
        new(
            "Securite tenant",
            "Administration des profils et des droits du tenant.",
            false,
            [
                TenantPermissionKeys.DashboardView,
                TenantPermissionKeys.SettingsAccessProfilesManage
            ]),
    ];

    public async Task<TenantAccessProfileSnapshot> GetSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            throw new NotFoundException(nameof(Tenant), tenantId);
        }

        var profiles = await dbContext.TenantAccessProfiles
            .AsNoTracking()
            .Include(x => x.Permissions)
            .Include(x => x.UserAssignments)
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var assignmentsByUser = await dbContext.TenantUserAccessProfiles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .GroupBy(x => x.UserId)
            .ToDictionaryAsync(
                x => x.Key,
                x => (IReadOnlyList<Guid>)x.Select(item => item.TenantAccessProfileId).Distinct().ToArray(),
                cancellationToken);

        var userItems = new List<TenantAccessUserItem>(users.Count);
        foreach (var user in users)
        {
            var trackedUser = await userManager.FindByIdAsync(user.Id);
            if (trackedUser is null)
            {
                continue;
            }

            var roles = await userManager.GetRolesAsync(trackedUser);
            userItems.Add(new TenantAccessUserItem(
                user.Id,
                user.Email ?? string.Empty,
                BuildDisplayName(user.FirstName, user.LastName, user.Email ?? string.Empty),
                roles.OrderBy(static role => role).ToArray(),
                assignmentsByUser.TryGetValue(user.Id, out var assignedProfileIds) ? assignedProfileIds : []));
        }

        var profileItems = profiles
            .Select(x => new TenantAccessProfileItem(
                x.Id,
                x.Name,
                x.Description,
                x.IsDefault,
                x.UserAssignments.Count,
                x.Permissions
                    .Select(permission => permission.PermissionKey)
                    .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();

        return new TenantAccessProfileSnapshot(
            tenant.Id,
            tenant.CompanyName,
            profileItems,
            userItems,
            TenantPermissionCatalog.PermissionGroups);
    }

    public async Task<Guid> SaveProfileAsync(Guid tenantId, TenantAccessProfileUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var permissionKeys = NormalizePermissionKeys(request.PermissionKeys);
        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["Name"] = new[] { "Le nom du profil est obligatoire." },
            });
        }

        var duplicateNameExists = await dbContext.TenantAccessProfiles
            .AnyAsync(
                x => x.TenantId == tenantId
                     && x.Name == normalizedName
                     && (!request.ProfileId.HasValue || x.Id != request.ProfileId.Value),
                cancellationToken);

        if (duplicateNameExists)
        {
            throw new BusinessRuleException(
                "Un profil portant ce nom existe deja.",
                errorCode: "ACCESS_PROFILE_NAME_DUPLICATE");
        }

        TenantAccessProfile profile;
        if (request.ProfileId.HasValue)
        {
            profile = await dbContext.TenantAccessProfiles
                .Include(x => x.Permissions)
                .FirstOrDefaultAsync(x => x.Id == request.ProfileId.Value && x.TenantId == tenantId, cancellationToken)
                ?? throw new NotFoundException(nameof(TenantAccessProfile), request.ProfileId.Value);
        }
        else
        {
            profile = new TenantAccessProfile
            {
                TenantId = tenantId
            };
            dbContext.TenantAccessProfiles.Add(profile);
        }

        profile.Name = normalizedName;
        profile.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        profile.IsDefault = request.IsDefault;

        if (request.IsDefault)
        {
            var otherDefaultProfiles = await dbContext.TenantAccessProfiles
                .Where(x => x.TenantId == tenantId && x.Id != profile.Id && x.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var otherProfile in otherDefaultProfiles)
            {
                otherProfile.IsDefault = false;
            }
        }

        var existingPermissions = profile.Permissions
            .ToDictionary(x => x.PermissionKey, StringComparer.OrdinalIgnoreCase);

        foreach (var permission in profile.Permissions.Where(x => !permissionKeys.Contains(x.PermissionKey, StringComparer.OrdinalIgnoreCase)).ToArray())
        {
            dbContext.TenantAccessProfilePermissions.Remove(permission);
        }

        foreach (var permissionKey in permissionKeys)
        {
            if (existingPermissions.ContainsKey(permissionKey))
            {
                continue;
            }

            profile.Permissions.Add(new TenantAccessProfilePermission
            {
                PermissionKey = permissionKey
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return profile.Id;
    }

    public async Task DeleteProfileAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.TenantAccessProfiles
            .Include(x => x.Permissions)
            .Include(x => x.UserAssignments)
            .FirstOrDefaultAsync(x => x.Id == profileId && x.TenantId == tenantId, cancellationToken);

        if (profile is null)
        {
            throw new NotFoundException(nameof(TenantAccessProfile), profileId);
        }

        dbContext.TenantAccessProfiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserAssignmentsAsync(Guid tenantId, TenantAccessUserAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.UserId && x.TenantId == tenantId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("TenantUser", request.UserId);
        }

        var normalizedProfileIds = request.ProfileIds.Distinct().ToArray();
        if (normalizedProfileIds.Length > 0)
        {
            var matchingProfileIds = await dbContext.TenantAccessProfiles
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDefault && normalizedProfileIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (matchingProfileIds.Count != normalizedProfileIds.Length)
            {
                throw new BusinessRuleException(
                    "Une ou plusieurs affectations ciblent un profil invalide.",
                    errorCode: "ACCESS_PROFILE_ASSIGNMENT_INVALID");
            }
        }

        var existingAssignments = await dbContext.TenantUserAccessProfiles
            .Where(x => x.TenantId == tenantId && x.UserId == request.UserId)
            .ToListAsync(cancellationToken);

        dbContext.TenantUserAccessProfiles.RemoveRange(existingAssignments);

        foreach (var profileId in normalizedProfileIds)
        {
            dbContext.TenantUserAccessProfiles.Add(new TenantUserAccessProfile
            {
                TenantId = tenantId,
                UserId = request.UserId,
                TenantAccessProfileId = profileId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<string>> GetEffectivePermissionKeysAsync(Guid tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.TenantId != tenantId)
        {
            return [];
        }

        var roles = await userManager.GetRolesAsync(user);
        if (roles.Any(static role => string.Equals(role, "PlatformAdmin", StringComparison.OrdinalIgnoreCase))
            || roles.Any(static role => string.Equals(role, "TenantOwner", StringComparison.OrdinalIgnoreCase)))
        {
            return TenantPermissionCatalog.AllKeys;
        }

        var permissions = new HashSet<string>(
            TenantPermissionCatalog.GetDefaultPermissionsForRoles(roles),
            StringComparer.OrdinalIgnoreCase);

        var defaultProfileIds = await dbContext.TenantAccessProfiles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsDefault)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var assignedProfileIds = await dbContext.TenantUserAccessProfiles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .Select(x => x.TenantAccessProfileId)
            .ToListAsync(cancellationToken);

        var effectiveProfileIds = defaultProfileIds
            .Concat(assignedProfileIds)
            .Distinct()
            .ToArray();

        if (effectiveProfileIds.Length == 0)
        {
            return permissions.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        var profilePermissionKeys = await dbContext.TenantAccessProfilePermissions
            .AsNoTracking()
            .Where(x => effectiveProfileIds.Contains(x.TenantAccessProfileId))
            .Select(x => x.PermissionKey)
            .ToListAsync(cancellationToken);

        permissions.UnionWith(profilePermissionKeys);
        return permissions.OrderBy(static key => key, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<int> EnsureStandardProfilesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var existingProfiles = await dbContext.TenantAccessProfiles
            .Include(x => x.Permissions)
            .Where(x => x.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var createdOrUpdatedCount = 0;

        foreach (var definition in StandardProfiles)
        {
            var profile = existingProfiles.FirstOrDefault(x => string.Equals(x.Name, definition.Name, StringComparison.OrdinalIgnoreCase));
            var wasCreated = false;
            if (profile is null)
            {
                profile = new TenantAccessProfile
                {
                    TenantId = tenantId,
                    Name = definition.Name
                };
                dbContext.TenantAccessProfiles.Add(profile);
                existingProfiles.Add(profile);
                wasCreated = true;
            }

            var changed = false;
            if (!string.Equals(profile.Description, definition.Description, StringComparison.Ordinal))
            {
                profile.Description = definition.Description;
                changed = true;
            }

            if (profile.IsDefault != definition.IsDefault)
            {
                profile.IsDefault = definition.IsDefault;
                changed = true;
            }

            var desiredKeys = definition.PermissionKeys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var existingKeys = profile.Permissions
                .Select(x => x.PermissionKey)
                .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var permission in profile.Permissions.Where(x => !desiredKeys.Contains(x.PermissionKey, StringComparer.OrdinalIgnoreCase)).ToArray())
            {
                dbContext.TenantAccessProfilePermissions.Remove(permission);
                changed = true;
            }

            foreach (var permissionKey in desiredKeys.Where(key => !existingKeys.Contains(key, StringComparer.OrdinalIgnoreCase)))
            {
                profile.Permissions.Add(new TenantAccessProfilePermission
                {
                    PermissionKey = permissionKey
                });
                changed = true;
            }

            if (wasCreated || changed)
            {
                createdOrUpdatedCount += 1;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return createdOrUpdatedCount;
    }

    private static IReadOnlyList<string> NormalizePermissionKeys(IReadOnlyList<string> permissionKeys)
    {
        var normalizedKeys = permissionKeys
            .Where(static key => !string.IsNullOrWhiteSpace(key))
            .Select(static key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var unknownPermissions = normalizedKeys
            .Where(permissionKey => !TenantPermissionCatalog.IsKnownPermission(permissionKey))
            .ToArray();

        if (unknownPermissions.Length > 0)
        {
            var ex = new BusinessRuleException(
                $"Permissions invalides : {string.Join(", ", unknownPermissions)}.",
                errorCode: "ACCESS_PERMISSION_UNKNOWN");
            ex.Data["unknownPermissions"] = unknownPermissions;
            throw ex;
        }

        return normalizedKeys;
    }

    private static string BuildDisplayName(string? firstName, string? lastName, string fallbackEmail)
    {
        var values = new[] { firstName, lastName }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();

        return values.Length > 0 ? string.Join(' ', values) : fallbackEmail;
    }

    private sealed record StandardAccessProfileDefinition(
        string Name,
        string Description,
        bool IsDefault,
        IReadOnlyList<string> PermissionKeys);
}
