using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.Commercial;

public class TaxCode : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Rate { get; set; }
}
