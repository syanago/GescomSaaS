using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.SaaS;

public class TenantUserAccessProfile : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid TenantAccessProfileId { get; set; }

    public Tenant? Tenant { get; set; }
    public TenantAccessProfile? TenantAccessProfile { get; set; }
}
