namespace GescomSaas.Application.Models;

public sealed record TenantUserItem(
    string UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);
