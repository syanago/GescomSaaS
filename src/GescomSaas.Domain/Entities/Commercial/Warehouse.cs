using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.Commercial;

public class Warehouse : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}
