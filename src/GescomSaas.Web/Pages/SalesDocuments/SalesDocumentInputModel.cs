using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.SalesDocuments;

public class SalesDocumentInputModel
{
    [Display(Name = "Type de piece")]
    public CommercialDocumentType DocumentType { get; set; } = CommercialDocumentType.SalesQuote;

    [Display(Name = "Numero")]
    public string Number { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Client")]
    public Guid? PartnerId { get; set; }

    [Display(Name = "Depot")]
    public Guid? WarehouseId { get; set; }

    [Display(Name = "Date document")]
    [DataType(DataType.Date)]
    public DateOnly DocumentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Display(Name = "Date echeance")]
    [DataType(DataType.Date)]
    public DateOnly? DueDate { get; set; }

    [StringLength(3)]
    [Display(Name = "Devise")]
    public string CurrencyCode { get; set; } = string.Empty;

    [Display(Name = "Statut")]
    public CommercialDocumentStatus Status { get; set; } = CommercialDocumentStatus.Draft;

    [Display(Name = "Notes")]
    public string? Notes { get; set; }

    public static SalesDocumentInputModel FromEntity(CommercialDocument entity) =>
        new()
        {
            DocumentType = entity.DocumentType,
            Number = entity.Number,
            PartnerId = entity.PartnerId,
            WarehouseId = entity.WarehouseId,
            DocumentDate = entity.DocumentDate,
            DueDate = entity.DueDate,
            CurrencyCode = entity.CurrencyCode,
            Status = entity.Status,
            Notes = entity.Notes
        };

    public void ApplyTo(CommercialDocument entity)
    {
        entity.DocumentType = DocumentType;
        entity.PartnerId = PartnerId ?? Guid.Empty;
        entity.WarehouseId = WarehouseId;
        entity.DocumentDate = DocumentDate;
        entity.DueDate = DueDate;
        entity.CurrencyCode = string.IsNullOrWhiteSpace(CurrencyCode)
            ? entity.CurrencyCode
            : CurrencyCode.Trim().ToUpperInvariant();
        entity.Status = Status;
        entity.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();
    }
}
