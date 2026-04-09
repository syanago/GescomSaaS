using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.SaaS;

public class PlatformInvoiceLine : AuditableEntity
{
    public Guid PlatformInvoiceId { get; set; }
    public PlatformInvoice? PlatformInvoice { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPriceExcludingTax { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotalExcludingTax { get; set; }
    public decimal LineTaxAmount { get; set; }
    public decimal LineTotalIncludingTax { get; set; }
}
