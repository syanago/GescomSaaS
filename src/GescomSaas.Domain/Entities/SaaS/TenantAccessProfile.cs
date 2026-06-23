using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.SaaS;

public class TenantAccessProfile : AuditableEntity
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }

    public Tenant? Tenant { get; set; }
    public ICollection<TenantAccessProfilePermission> Permissions { get; set; } = new List<TenantAccessProfilePermission>();
    public ICollection<TenantUserAccessProfile> UserAssignments { get; set; } = new List<TenantUserAccessProfile>();
}
