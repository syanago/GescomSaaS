using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.Commercial;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Partners;

public class PartnerInputModel
{
    [Required]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Raison sociale")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Type de tiers")]
    public BusinessPartnerType PartnerType { get; set; } = BusinessPartnerType.Customer;

    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Display(Name = "Telephone")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Numero TVA")]
    public string? VatNumber { get; set; }

    [Display(Name = "Plafond de credit")]
    public decimal CreditLimit { get; set; }

    [Display(Name = "Condition de paiement")]
    public Guid? PaymentTermId { get; set; }

    [Display(Name = "Actif")]
    public bool IsActive { get; set; } = true;

    public AddressInputModel BillingAddress { get; set; } = new();
    public AddressInputModel ShippingAddress { get; set; } = new();

    public static PartnerInputModel FromEntity(BusinessPartner entity) =>
        new()
        {
            Code = entity.Code,
            Name = entity.Name,
            PartnerType = entity.PartnerType,
            Email = entity.Email,
            PhoneNumber = entity.PhoneNumber,
            VatNumber = entity.VatNumber,
            CreditLimit = entity.CreditLimit,
            PaymentTermId = entity.PaymentTermId,
            IsActive = entity.IsActive,
            BillingAddress = AddressInputModel.FromEntity(entity.BillingAddress),
            ShippingAddress = AddressInputModel.FromEntity(entity.ShippingAddress)
        };

    public void ApplyTo(BusinessPartner entity)
    {
        entity.Code = Code.Trim();
        entity.Name = Name.Trim();
        entity.PartnerType = PartnerType;
        entity.Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        entity.PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim();
        entity.VatNumber = string.IsNullOrWhiteSpace(VatNumber) ? null : VatNumber.Trim();
        entity.CreditLimit = CreditLimit;
        entity.PaymentTermId = PaymentTermId;
        entity.IsActive = IsActive;
        entity.BillingAddress = BillingAddress.ToEntity();
        entity.ShippingAddress = ShippingAddress.ToEntity();
    }
}

public class AddressInputModel
{
    public string? Recipient { get; set; }
    public string? StreetLine1 { get; set; }
    public string? StreetLine2 { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }

    public static AddressInputModel FromEntity(Address entity) =>
        new()
        {
            Recipient = entity.Recipient,
            StreetLine1 = entity.StreetLine1,
            StreetLine2 = entity.StreetLine2,
            PostalCode = entity.PostalCode,
            City = entity.City,
            State = entity.State,
            Country = entity.Country
        };

    public Address ToEntity() =>
        new()
        {
            Recipient = TrimOrNull(Recipient),
            StreetLine1 = TrimOrNull(StreetLine1),
            StreetLine2 = TrimOrNull(StreetLine2),
            PostalCode = TrimOrNull(PostalCode),
            City = TrimOrNull(City),
            State = TrimOrNull(State),
            Country = TrimOrNull(Country)
        };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
