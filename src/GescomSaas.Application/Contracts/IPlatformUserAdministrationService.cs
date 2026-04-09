using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface IPlatformUserAdministrationService
{
    Task<TenantUserManagementSnapshot> GetTenantSnapshotAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<string> CreateInvitationAsync(Guid tenantId, UserInvitationRequest request, CancellationToken cancellationToken = default);
    Task AttachExistingUserAsync(Guid tenantId, string userId, IReadOnlyList<string> roles, CancellationToken cancellationToken = default);
    Task UpdateUserAsync(Guid tenantId, string userId, TenantUserUpdateRequest request, CancellationToken cancellationToken = default);
    Task DetachUserAsync(Guid tenantId, string userId, CancellationToken cancellationToken = default);
    Task CancelInvitationAsync(Guid tenantId, Guid invitationId, CancellationToken cancellationToken = default);
    Task<InvitationAcceptanceContext?> GetInvitationAsync(string token, CancellationToken cancellationToken = default);
    Task AcceptInvitationAsync(string token, InvitationAcceptanceRequest request, CancellationToken cancellationToken = default);
}
