using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record InvitationAcceptanceContext(
    string Token,
    string TenantName,
    string Email,
    string? FirstName,
    string? LastName,
    IReadOnlyList<string> Roles,
    UserInvitationStatus Status,
    DateTime ExpiresOnUtc,
    bool RequiresPassword);
