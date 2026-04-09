using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.Commercial;

public class PriceList : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "CAD";
    public bool IsDefault { get; set; }

    public ICollection<PriceListLine> Lines { get; set; } = new List<PriceListLine>();
}
