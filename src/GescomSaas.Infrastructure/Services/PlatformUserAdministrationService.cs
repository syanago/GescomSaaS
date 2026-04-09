using System.Security.Cryptography;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Identity;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class PlatformUserAdministrationService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ITenantQuotaEnforcementService tenantQuotaEnforcementService,
    LinkGenerator linkGenerator,
    IHttpContextAccessor httpContextAccessor) : IPlatformUserAdministrationService
{
    private static readonly string[] AssignableTenantRoles =
    [
        "TenantOwner",
        "SalesManager",
        "PurchasingManager",
        "FinanceManager",
        "InventoryManager"
    ];

    public async Task<TenantUserManagementSnapshot> GetTenantSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            throw new InvalidOperationException("Tenant introuvable.");
        }

        await EnsureTenantRolesExistAsync();

        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Email)
            .Select(x => new TenantScopedUserProjection(
                x.Id,
                x.Email ?? string.Empty,
                x.FirstName,
                x.LastName))
            .ToListAsync(cancellationToken);

        var userItems = new List<TenantUserItem>(users.Count);
        foreach (var user in users)
        {
            var trackedUser = await userManager.FindByIdAsync(user.UserId);
            if (trackedUser is null)
            {
                continue;
            }

            var roles = await userManager.GetRolesAsync(trackedUser);
            userItems.Add(new TenantUserItem(
                user.UserId,
                user.Email,
                BuildDisplayName(user.FirstName, user.LastName, user.Email),
                roles.Where(IsTenantRole)
                    .OrderBy(GetRoleOrder)
                    .ToList()));
        }

        var availableUserRows = await userManager.Users
            .AsNoTracking()
            .Where(x => !x.TenantId.HasValue)
            .OrderBy(x => x.Email)
            .Select(x => new TenantScopedUserProjection(
                x.Id,
                x.Email ?? string.Empty,
                x.FirstName,
                x.LastName))
            .ToListAsync(cancellationToken);

        var availableUsers = availableUserRows
            .Select(x => new AvailableUserItem(
                x.UserId,
                x.Email,
                BuildDisplayName(x.FirstName, x.LastName, x.Email)))
            .ToList();

        var availableUserEmails = availableUsers
            .Select(x => x.Email.Trim())
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var utcNow = DateTime.UtcNow;
        var invitations = await dbContext.UserInvitations
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Status != UserInvitationStatus.Accepted && x.Status != UserInvitationStatus.Cancelled)
            .OrderBy(x => x.ExpiresOnUtc)
            .ThenBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var invitationItems = invitations
            .Select(x =>
            {
                var effectiveStatus = GetEffectiveInvitationStatus(x, utcNow);
                return new PendingInvitationItem(
                    x.Id,
                    x.Email,
                    BuildDisplayName(x.FirstName, x.LastName, x.Email),
                    ParseRoles(x.RequestedRoles),
                    effectiveStatus,
                    x.ExpiresOnUtc,
                    BuildInvitationUrl(x.InvitationToken),
                    availableUserEmails.Contains(x.Email));
            })
            .ToList();

        return new TenantUserManagementSnapshot(
            tenant.Id,
            tenant.CompanyName,
            userItems,
            invitationItems,
            availableUsers,
            AssignableTenantRoles);
    }

    public async Task<string> CreateInvitationAsync(Guid tenantId, UserInvitationRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            throw new InvalidOperationException("Tenant introuvable.");
        }

        await EnsureTenantRolesExistAsync();

        var email = NormalizeEmail(request.Email);
        var roles = await NormalizeRequestedRolesAsync(request.Roles);

        var existingUser = await userManager.FindByEmailAsync(email);
        if (existingUser?.TenantId.HasValue == true && existingUser.TenantId != tenantId)
        {
            throw new InvalidOperationException("Cet utilisateur est deja rattache a un autre tenant.");
        }

        if (existingUser?.TenantId == tenantId)
        {
            throw new InvalidOperationException("Cet utilisateur est deja rattache a ce tenant.");
        }

        var hasPendingInvitation = await dbContext.UserInvitations
            .AnyAsync(
                x => x.TenantId == tenantId &&
                     x.Email == email &&
                     x.Status == UserInvitationStatus.Pending &&
                     x.ExpiresOnUtc > DateTime.UtcNow,
                cancellationToken);

        if (hasPendingInvitation)
        {
            throw new InvalidOperationException("Une invitation en cours existe deja pour cette adresse e-mail.");
        }

        var invitation = new UserInvitation
        {
            TenantId = tenantId,
            Email = email,
            FirstName = NormalizeOptionalText(request.FirstName),
            LastName = NormalizeOptionalText(request.LastName),
            InvitationToken = GenerateInvitationToken(),
            RequestedRoles = string.Join(',', roles),
            Status = UserInvitationStatus.Pending,
            ExpiresOnUtc = DateTime.UtcNow.AddDays(7),
            Notes = NormalizeOptionalText(request.Notes),
            ApplicationUserId = existingUser?.Id
        };

        dbContext.UserInvitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return BuildInvitationUrl(invitation.InvitationToken);
    }

    public async Task AttachExistingUserAsync(Guid tenantId, string userId, IReadOnlyList<string> roles, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            throw new InvalidOperationException("Utilisateur introuvable.");
        }

        if (user.TenantId.HasValue && user.TenantId != tenantId)
        {
            throw new InvalidOperationException("Cet utilisateur est deja rattache a un autre tenant.");
        }

        if (!await dbContext.Tenants.AsNoTracking().AnyAsync(x => x.Id == tenantId, cancellationToken))
        {
            throw new InvalidOperationException("Tenant introuvable.");
        }

        var normalizedRoles = await NormalizeRequestedRolesAsync(roles);

        if (!user.TenantId.HasValue)
        {
            await tenantQuotaEnforcementService.EnsureCanManageUsersAsync(tenantId, cancellationToken: cancellationToken);
        }

        user.TenantId = tenantId;
        user.EmailConfirmed = true;

        await UpdateIdentityUserAsync(user);
        await ReplaceTenantRolesAsync(user, normalizedRoles);

        var pendingInvitations = await dbContext.UserInvitations
            .Where(x => x.TenantId == tenantId && x.Email == (user.Email ?? string.Empty) && x.Status == UserInvitationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var invitation in pendingInvitations)
        {
            invitation.Status = UserInvitationStatus.Cancelled;
            invitation.CancelledOnUtc = DateTime.UtcNow;
            invitation.ApplicationUserId = user.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateUserAsync(Guid tenantId, string userId, TenantUserUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.TenantId != tenantId)
        {
            throw new InvalidOperationException("Utilisateur de tenant introuvable.");
        }

        var normalizedRoles = await NormalizeRequestedRolesAsync(request.Roles);
        await EnsureTenantOwnerWillRemainAsync(tenantId, user, normalizedRoles, cancellationToken);

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();

        await UpdateIdentityUserAsync(user);
        await ReplaceTenantRolesAsync(user, normalizedRoles);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DetachUserAsync(Guid tenantId, string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.TenantId != tenantId)
        {
            throw new InvalidOperationException("Utilisateur de tenant introuvable.");
        }

        await EnsureTenantOwnerWillRemainAsync(tenantId, user, [], cancellationToken);

        user.TenantId = null;
        await UpdateIdentityUserAsync(user);

        var currentRoles = await userManager.GetRolesAsync(user);
        var tenantRolesToRemove = currentRoles.Where(IsTenantRole).ToArray();
        if (tenantRolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, tenantRolesToRemove);
            EnsureIdentityResult(removeResult, "Impossible de retirer les roles de l'utilisateur.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelInvitationAsync(Guid tenantId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var invitation = await dbContext.UserInvitations
            .FirstOrDefaultAsync(x => x.Id == invitationId && x.TenantId == tenantId, cancellationToken);

        if (invitation is null)
        {
            throw new InvalidOperationException("Invitation introuvable.");
        }

        if (invitation.Status == UserInvitationStatus.Accepted)
        {
            throw new InvalidOperationException("Cette invitation a deja ete acceptee.");
        }

        invitation.Status = UserInvitationStatus.Cancelled;
        invitation.CancelledOnUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InvitationAcceptanceContext?> GetInvitationAsync(string token, CancellationToken cancellationToken = default)
    {
        token = token.Trim();
        var invitation = await dbContext.UserInvitations
            .AsNoTracking()
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.InvitationToken == token, cancellationToken);

        if (invitation is null || invitation.Tenant is null)
        {
            return null;
        }

        var existingUser = await userManager.FindByEmailAsync(invitation.Email);
        var requiresPassword = existingUser is null || !await userManager.HasPasswordAsync(existingUser);

        return new InvitationAcceptanceContext(
            invitation.InvitationToken,
            invitation.Tenant.CompanyName,
            invitation.Email,
            invitation.FirstName,
            invitation.LastName,
            ParseRoles(invitation.RequestedRoles),
            GetEffectiveInvitationStatus(invitation, DateTime.UtcNow),
            invitation.ExpiresOnUtc,
            requiresPassword);
    }

    public async Task AcceptInvitationAsync(string token, InvitationAcceptanceRequest request, CancellationToken cancellationToken = default)
    {
        token = token.Trim();
        var invitation = await dbContext.UserInvitations
            .Include(x => x.Tenant)
            .FirstOrDefaultAsync(x => x.InvitationToken == token, cancellationToken);

        if (invitation is null || invitation.Tenant is null)
        {
            throw new InvalidOperationException("Invitation introuvable.");
        }

        if (invitation.Status == UserInvitationStatus.Accepted)
        {
            throw new InvalidOperationException("Cette invitation a deja ete acceptee.");
        }

        if (invitation.Status == UserInvitationStatus.Cancelled)
        {
            throw new InvalidOperationException("Cette invitation a ete annulee.");
        }

        if (invitation.ExpiresOnUtc <= DateTime.UtcNow)
        {
            invitation.Status = UserInvitationStatus.Expired;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException("Cette invitation a expire.");
        }

        var roles = await NormalizeRequestedRolesAsync(ParseRoles(invitation.RequestedRoles));
        var email = NormalizeEmail(invitation.Email);

        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && user.TenantId.HasValue && user.TenantId != invitation.TenantId)
        {
            throw new InvalidOperationException("Ce compte utilisateur est deja rattache a un autre tenant.");
        }

        if (user is null)
        {
            await tenantQuotaEnforcementService.EnsureCanManageUsersAsync(invitation.TenantId, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new InvalidOperationException("Un mot de passe est requis pour creer le compte.");
            }

            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                TenantId = invitation.TenantId,
                FirstName = FirstNonEmpty(request.FirstName, invitation.FirstName),
                LastName = FirstNonEmpty(request.LastName, invitation.LastName)
            };

            var createResult = await userManager.CreateAsync(user, request.Password);
            EnsureIdentityResult(createResult, "Impossible de creer le compte utilisateur.");
        }
        else
        {
            if (!user.TenantId.HasValue)
            {
                await tenantQuotaEnforcementService.EnsureCanManageUsersAsync(invitation.TenantId, cancellationToken: cancellationToken);
                user.TenantId = invitation.TenantId;
            }

            user.FirstName = FirstNonEmpty(request.FirstName, invitation.FirstName, user.FirstName);
            user.LastName = FirstNonEmpty(request.LastName, invitation.LastName, user.LastName);
            user.EmailConfirmed = true;

            await UpdateIdentityUserAsync(user);

            if (!await userManager.HasPasswordAsync(user))
            {
                if (string.IsNullOrWhiteSpace(request.Password))
                {
                    throw new InvalidOperationException("Un mot de passe est requis pour finaliser l'invitation.");
                }

                var addPasswordResult = await userManager.AddPasswordAsync(user, request.Password);
                EnsureIdentityResult(addPasswordResult, "Impossible d'ajouter le mot de passe du compte.");
            }
        }

        await ReplaceTenantRolesAsync(user, roles);

        invitation.Status = UserInvitationStatus.Accepted;
        invitation.AcceptedOnUtc = DateTime.UtcNow;
        invitation.ApplicationUserId = user.Id;

        var siblingInvitations = await dbContext.UserInvitations
            .Where(x => x.Id != invitation.Id &&
                        x.TenantId == invitation.TenantId &&
                        x.Email == email &&
                        x.Status == UserInvitationStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var siblingInvitation in siblingInvitations)
        {
            siblingInvitation.Status = UserInvitationStatus.Cancelled;
            siblingInvitation.CancelledOnUtc = DateTime.UtcNow;
            siblingInvitation.ApplicationUserId = user.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureTenantRolesExistAsync()
    {
        foreach (var roleName in AssignableTenantRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                EnsureIdentityResult(result, $"Impossible de creer le role {roleName}.");
            }
        }
    }

    private async Task<IReadOnlyList<string>> NormalizeRequestedRolesAsync(IReadOnlyList<string> roles)
    {
        var normalizedRoles = roles
            .Where(static role => !string.IsNullOrWhiteSpace(role))
            .Select(static role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetRoleOrder)
            .ToList();

        if (normalizedRoles.Count == 0)
        {
            throw new InvalidOperationException("Selectionnez au moins un role.");
        }

        var invalidRoles = normalizedRoles.Where(role => !IsTenantRole(role)).ToList();
        if (invalidRoles.Count > 0)
        {
            throw new InvalidOperationException($"Roles invalides: {string.Join(", ", invalidRoles)}.");
        }

        await EnsureTenantRolesExistAsync();
        return normalizedRoles;
    }

    private async Task ReplaceTenantRolesAsync(ApplicationUser user, IReadOnlyList<string> desiredRoles)
    {
        var currentRoles = await userManager.GetRolesAsync(user);
        var tenantRoles = currentRoles.Where(IsTenantRole).ToArray();

        var rolesToRemove = tenantRoles
            .Where(currentRole => desiredRoles.All(desiredRole => !string.Equals(desiredRole, currentRole, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (rolesToRemove.Length > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            EnsureIdentityResult(removeResult, "Impossible de retirer certains roles.");
        }

        var rolesToAdd = desiredRoles
            .Where(desiredRole => currentRoles.All(currentRole => !string.Equals(currentRole, desiredRole, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (rolesToAdd.Length > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            EnsureIdentityResult(addResult, "Impossible d'attribuer certains roles.");
        }
    }

    private async Task EnsureTenantOwnerWillRemainAsync(Guid tenantId, ApplicationUser user, IReadOnlyList<string> desiredRoles, CancellationToken cancellationToken)
    {
        var currentRoles = await userManager.GetRolesAsync(user);
        var userIsOwner = currentRoles.Any(role => string.Equals(role, "TenantOwner", StringComparison.OrdinalIgnoreCase));
        var willRemainOwner = desiredRoles.Any(role => string.Equals(role, "TenantOwner", StringComparison.OrdinalIgnoreCase));

        if (!userIsOwner || willRemainOwner)
        {
            return;
        }

        var ownerCount = await (
            from userRole in dbContext.UserRoles
            join dbUser in dbContext.Users on userRole.UserId equals dbUser.Id
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where dbUser.TenantId == tenantId && role.Name == "TenantOwner"
            select dbUser.Id)
            .Distinct()
            .CountAsync(cancellationToken);

        if (ownerCount <= 1)
        {
            throw new InvalidOperationException("Le tenant doit conserver au moins un TenantOwner.");
        }
    }

    private async Task UpdateIdentityUserAsync(ApplicationUser user)
    {
        var updateResult = await userManager.UpdateAsync(user);
        EnsureIdentityResult(updateResult, "Impossible de mettre a jour l'utilisateur.");
    }

    private string BuildInvitationUrl(string token)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var absoluteUri = httpContext is null
            ? null
            : linkGenerator.GetUriByPage(httpContext, page: "/Invitations/Accept", values: new { token });

        return absoluteUri ?? $"/Invitations/Accept?token={Uri.EscapeDataString(token)}";
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("L'adresse e-mail est obligatoire.");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string GenerateInvitationToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static IReadOnlyList<string> ParseRoles(string requestedRoles) =>
        requestedRoles
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetRoleOrder)
            .ToList();

    private static UserInvitationStatus GetEffectiveInvitationStatus(UserInvitation invitation, DateTime utcNow)
    {
        if (invitation.Status == UserInvitationStatus.Pending && invitation.ExpiresOnUtc <= utcNow)
        {
            return UserInvitationStatus.Expired;
        }

        return invitation.Status;
    }

    private static string BuildDisplayName(string? firstName, string? lastName, string fallbackEmail)
    {
        var values = new[] { firstName, lastName }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();

        return values.Length > 0 ? string.Join(' ', values) : fallbackEmail;
    }

    private static bool IsTenantRole(string role) =>
        AssignableTenantRoles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private static int GetRoleOrder(string role)
    {
        for (var index = 0; index < AssignableTenantRoles.Length; index++)
        {
            if (string.Equals(AssignableTenantRoles[index], role, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return int.MaxValue;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static void EnsureIdentityResult(IdentityResult result, string message)
    {
        if (result.Succeeded)
        {
            return;
        }

        var details = string.Join(", ", result.Errors.Select(x => x.Description));
        throw new InvalidOperationException($"{message} {details}".Trim());
    }

    private sealed record TenantScopedUserProjection(
        string UserId,
        string Email,
        string FirstName,
        string LastName);
}
