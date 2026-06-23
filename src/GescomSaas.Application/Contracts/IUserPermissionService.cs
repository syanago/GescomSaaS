using System.Security.Claims;

namespace GescomSaas.Application.Contracts;

public interface IUserPermissionService
{
    Task<IReadOnlyCollection<string>> GetCurrentPermissionKeysAsync(CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(string permissionKey, CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(ClaimsPrincipal principal, string permissionKey, CancellationToken cancellationToken = default);
    Task<bool> HasAnyPermissionAsync(ClaimsPrincipal principal, IReadOnlyCollection<string> permissionKeys, CancellationToken cancellationToken = default);
}
