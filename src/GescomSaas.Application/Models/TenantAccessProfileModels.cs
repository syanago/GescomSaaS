namespace GescomSaas.Application.Models;

public sealed record TenantAccessProfileItem(
    Guid Id,
    string Name,
    string? Description,
    bool IsDefault,
    int AssignedUserCount,
    IReadOnlyList<string> PermissionKeys);

public sealed record TenantAccessUserItem(
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> AssignedProfileIds);

public sealed record TenantAccessPermissionGroup(
    string Name,
    IReadOnlyList<TenantAccessPermissionItem> Permissions);

public sealed record TenantAccessPermissionItem(
    string Key,
    string Label,
    string Description);

public sealed record TenantAccessProfileSnapshot(
    Guid TenantId,
    string TenantName,
    IReadOnlyList<TenantAccessProfileItem> Profiles,
    IReadOnlyList<TenantAccessUserItem> Users,
    IReadOnlyList<TenantAccessPermissionGroup> PermissionGroups);

public sealed record TenantAccessProfileUpsertRequest(
    Guid? ProfileId,
    string Name,
    string? Description,
    bool IsDefault,
    IReadOnlyList<string> PermissionKeys);

public sealed record TenantAccessUserAssignmentRequest(
    string UserId,
    IReadOnlyList<Guid> ProfileIds);
