using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record PendingInvitationItem(
    Guid InvitationId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    UserInvitationStatus Status,
    DateTime ExpiresOnUtc,
    string InviteUrl,
    bool CanAttachImmediately);
