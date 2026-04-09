using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.Commercial;

public class ProductCategory : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public StockValuationMethod StockValuationMethod { get; set; } = StockValuationMethod.Cmup;
    public StockIdentityTrackingMode StockIdentityTrackingMode { get; set; } = StockIdentityTrackingMode.None;
}
