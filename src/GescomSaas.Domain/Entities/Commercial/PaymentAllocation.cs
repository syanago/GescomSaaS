using GescomSaas.Domain.Common;

namespace GescomSaas.Domain.Entities.Commercial;

public class PaymentAllocation : AuditableEntity
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public Guid CommercialDocumentId { get; set; }
    public CommercialDocument? CommercialDocument { get; set; }

    public decimal AllocatedAmount { get; set; }
    public DateTime AllocatedOnUtc { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
