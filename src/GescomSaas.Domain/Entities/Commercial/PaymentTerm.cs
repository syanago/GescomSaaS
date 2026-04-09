using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.Commercial;

public class PaymentTerm : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int DueInDays { get; set; }
}
