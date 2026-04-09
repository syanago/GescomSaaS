using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.Commercial;

public class CommercialDocumentLine : AuditableEntity
{
    public Guid CommercialDocumentId { get; set; }
    public CommercialDocument? CommercialDocument { get; set; }

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPriceExcludingTax { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotalExcludingTax { get; set; }
    public decimal LineTaxAmount { get; set; }
    public decimal LineTotalIncludingTax { get; set; }
}
