namespace GescomSaas.Application.Models;

public sealed record TenantUserUpdateRequest(
    string FirstName,
    string LastName,
    IReadOnlyList<string> Roles);
