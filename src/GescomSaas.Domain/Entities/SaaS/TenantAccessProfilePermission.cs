using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.SaaS;

public class TenantAccessProfilePermission : AuditableEntity
{
    public Guid TenantAccessProfileId { get; set; }
    public string PermissionKey { get; set; } = string.Empty;

    public TenantAccessProfile? TenantAccessProfile { get; set; }
}
