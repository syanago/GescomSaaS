using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.Commercial;

public class CommercialDocument : TenantEntity
{
    public CommercialDocumentType DocumentType { get; set; }
    public CommercialDocumentStatus Status { get; set; } = CommercialDocumentStatus.Draft;
    public string Number { get; set; } = string.Empty;
    public DateOnly DocumentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? DueDate { get; set; }
    public string CurrencyCode { get; set; } = "CAD";
    public string? Notes { get; set; }
    public Guid? SourceDocumentId { get; set; }
    public CommercialDocument? SourceDocument { get; set; }

    public Guid PartnerId { get; set; }
    public BusinessPartner? Partner { get; set; }

    public Guid? WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    public decimal TotalExcludingTax { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalIncludingTax { get; set; }

    public ICollection<CommercialDocumentLine> Lines { get; set; } = new List<CommercialDocumentLine>();
    public ICollection<CommercialDocument> DerivedDocuments { get; set; } = new List<CommercialDocument>();
    public ICollection<PaymentAllocation> PaymentAllocations { get; set; } = new List<PaymentAllocation>();
    public ICollection<ReminderLog> ReminderLogs { get; set; } = new List<ReminderLog>();
}
