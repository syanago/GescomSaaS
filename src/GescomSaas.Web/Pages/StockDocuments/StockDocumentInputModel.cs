using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.StockDocuments;

public class StockDocumentInputModel
{
    public StockDocumentType DocumentType { get; set; } = StockDocumentType.Entry;

    [Display(Name = "Numero")]
    public string Number { get; set; } = string.Empty;

    [Display(Name = "Statut")]
    public StockDocumentStatus Status { get; set; } = StockDocumentStatus.Draft;

    [Display(Name = "Date document")]
    [DataType(DataType.Date)]
    public DateOnly DocumentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Display(Name = "Depot source")]
    public Guid? SourceWarehouseId { get; set; }

    [Display(Name = "Depot destination")]
    public Guid? DestinationWarehouseId { get; set; }

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public static StockDocumentInputModel FromEntity(StockDocument entity) =>
        new()
        {
            DocumentType = entity.DocumentType,
            Number = entity.Number,
            Status = entity.Status,
            DocumentDate = entity.DocumentDate,
            SourceWarehouseId = entity.SourceWarehouseId,
            DestinationWarehouseId = entity.DestinationWarehouseId,
            Notes = entity.Notes
        };

    public void ApplyTo(StockDocument entity)
    {
        entity.DocumentType = DocumentType;
        entity.Number = Number.Trim();
        entity.Status = Status;
        entity.DocumentDate = DocumentDate;
        entity.SourceWarehouseId = SourceWarehouseId;
        entity.DestinationWarehouseId = DestinationWarehouseId;
        entity.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
    }
}
