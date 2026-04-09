using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.Commercial;

public class StockDocumentLine : AuditableEntity
{
    public Guid StockDocumentId { get; set; }
    public StockDocument? StockDocument { get; set; }

    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly? ExpirationDate { get; set; }
}
