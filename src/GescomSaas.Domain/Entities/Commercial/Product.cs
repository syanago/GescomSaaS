using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.Commercial;

public class Product : TenantEntity
{
    public string Sku { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProductType ProductType { get; set; } = ProductType.StockItem;
    public string UnitOfMeasure { get; set; } = "UN";
    public bool TrackStock { get; set; } = true;
    public StockValuationMethod StockValuationMethod { get; set; } = StockValuationMethod.Cmup;
    public StockIdentityTrackingMode StockIdentityTrackingMode { get; set; } = StockIdentityTrackingMode.None;
    public bool IsActive { get; set; } = true;

    public Guid? ProductCategoryId { get; set; }
    public ProductCategory? ProductCategory { get; set; }

    public Guid? TaxCodeId { get; set; }
    public TaxCode? TaxCode { get; set; }

    public decimal PurchasePrice { get; set; }
    public decimal SalesPrice { get; set; }

    public ICollection<StockMovement> StockMovements { get; set; } = new List<StockMovement>();
    public ICollection<PriceListLine> PriceListLines { get; set; } = new List<PriceListLine>();
}
