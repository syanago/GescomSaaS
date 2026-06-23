using System.Security.Claims;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace GescomSaas.Infrastructure.Services;

public class UserPermissionService(
    IHttpContextAccessor httpContextAccessor,
    ICurrentTenantAccessor currentTenantAccessor,
    UserManager<ApplicationUser> userManager,
    ITenantAccessProfileService tenantAccessProfileService) : IUserPermissionService
{
    private ClaimsPrincipal? cachedPrincipal;
    private IReadOnlyCollection<string>? cachedPermissions;

    public async Task<IReadOnlyCollection<string>> GetCurrentPermissionKeysAsync(CancellationToken cancellationToken = default)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return principal is null
            ? []
            : await GetPermissionsAsync(principal, cancellationToken);
    }

    public Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        return principal is null
            ? Task.FromResult(false)
            : HasPermissionAsync(principal, permissionKey, cancellationToken);
    }

    public async Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permissionKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return false;
        }

        var permissions = await GetPermissionsAsync(principal, cancellationToken);
        return permissions.Contains(permissionKey, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> HasAnyPermissionAsync(ClaimsPrincipal principal, IReadOnlyCollection<string> permissionKeys, CancellationToken cancellationToken = default)
    {
        if (permissionKeys.Count == 0)
        {
            return false;
        }

        var permissions = await GetPermissionsAsync(principal, cancellationToken);
        return permissionKeys.Any(permission => permissions.Contains(permission, StringComparer.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyCollection<string>> GetPermissionsAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (cachedPrincipal == principal && cachedPermissions is not null)
        {
            return cachedPermissions;
        }

        if (!(principal.Identity?.IsAuthenticated ?? false))
        {
            cachedPrincipal = principal;
            cachedPermissions = [];
            return cachedPermissions;
        }

        if (principal.IsInRole("PlatformAdmin"))
        {
            cachedPrincipal = principal;
            cachedPermissions = TenantPermissionCatalog.AllKeys;
            return cachedPermissions;
        }

        var userId = userManager.GetUserId(principal);
        var tenantId = ResolveTenantId(principal);
        if (string.IsNullOrWhiteSpace(userId) || !tenantId.HasValue)
        {
            cachedPrincipal = principal;
            cachedPermissions = [];
            return cachedPermissions;
        }

        cachedPrincipal = principal;
        cachedPermissions = await tenantAccessProfileService.GetEffectivePermissionKeysAsync(tenantId.Value, userId, cancellationToken);
        return cachedPermissions;
    }

    private Guid? ResolveTenantId(ClaimsPrincipal principal)
    {
        var claimValue = principal.FindFirstValue("tenant_id");
        if (Guid.TryParse(claimValue, out var tenantId))
        {
            return tenantId;
        }

        return currentTenantAccessor.GetTenantId();
    }
}
