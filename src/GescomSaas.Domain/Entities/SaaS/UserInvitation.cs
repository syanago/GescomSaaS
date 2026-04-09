using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.SaaS;

public class UserInvitation : TenantEntity
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string InvitationToken { get; set; } = string.Empty;
    public string RequestedRoles { get; set; } = string.Empty;
    public UserInvitationStatus Status { get; set; } = UserInvitationStatus.Pending;
    public DateTime ExpiresOnUtc { get; set; } = DateTime.UtcNow.AddDays(7);
    public DateTime? AcceptedOnUtc { get; set; }
    public DateTime? CancelledOnUtc { get; set; }
    public string? Notes { get; set; }

    public Tenant? Tenant { get; set; }

    public string? ApplicationUserId { get; set; }
}
