using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.Commercial;

public class StockDocument : TenantEntity
{
    public string Number { get; set; } = string.Empty;
    public StockDocumentType DocumentType { get; set; }
    public StockDocumentStatus Status { get; set; } = StockDocumentStatus.Draft;
    public DateOnly DocumentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public Guid? SourceWarehouseId { get; set; }
    public Warehouse? SourceWarehouse { get; set; }

    public Guid? DestinationWarehouseId { get; set; }
    public Warehouse? DestinationWarehouse { get; set; }

    public string? Notes { get; set; }
    public DateTime? PostedOnUtc { get; set; }

    public ICollection<StockDocumentLine> Lines { get; set; } = new List<StockDocumentLine>();
}
