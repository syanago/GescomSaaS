namespace GescomSaas.Application.Models;

public sealed record TenantUserManagementSnapshot(
    Guid TenantId,
    string TenantName,
    IReadOnlyList<TenantUserItem> Users,
    IReadOnlyList<PendingInvitationItem> Invitations,
    IReadOnlyList<AvailableUserItem> AvailableUsers,
    IReadOnlyList<string> AssignableRoles);
