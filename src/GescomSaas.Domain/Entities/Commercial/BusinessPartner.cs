using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.Commercial;

public class BusinessPartner : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BusinessPartnerType PartnerType { get; set; } = BusinessPartnerType.Customer;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? VatNumber { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? PaymentTermId { get; set; }
    public PaymentTerm? PaymentTerm { get; set; }

    public Address BillingAddress { get; set; } = new();
    public Address ShippingAddress { get; set; } = new();

    public ICollection<CommercialDocument> Documents { get; set; } = new List<CommercialDocument>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
