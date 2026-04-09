namespace GescomSaas.Domain.Common;

public abstract class TenantEntity : AuditableEntity
{
    public Guid TenantId { get; set; }
}
