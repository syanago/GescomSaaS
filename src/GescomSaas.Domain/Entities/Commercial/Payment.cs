using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.Commercial;

public class Payment : TenantEntity
{
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public PaymentDirection Direction { get; set; }
    public PaymentMethod Method { get; set; } = PaymentMethod.BankTransfer;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "CAD";
    public decimal Amount { get; set; }
    public string? Notes { get; set; }

    public Guid PartnerId { get; set; }
    public BusinessPartner? Partner { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}
