using GescomSaas.Domain.Common;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Domain.Entities.SaaS;

public class Tenant : AuditableEntity
{
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyLegalName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PrimaryContactEmail { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "CA";
    public string CurrencyCode { get; set; } = "CAD";
    public string CashCurrencyCode { get; set; } = "CAD";
    public string CurrencySymbol { get; set; } = "$";
    public CurrencySymbolPosition CurrencySymbolPosition { get; set; } = CurrencySymbolPosition.BeforeAmount;
    public string MoneyDecimalSeparator { get; set; } = ",";
    public string MoneyGroupSeparator { get; set; } = " ";
    public int MoneyDecimalPlaces { get; set; } = 2;
    public string QuantityDecimalSeparator { get; set; } = ",";
    public string QuantityGroupSeparator { get; set; } = " ";
    public int QuantityDecimalPlaces { get; set; } = 3;
    public bool AllowNegativeStock { get; set; }
    public StockValuationMethod DefaultStockValuationMethod { get; set; } = StockValuationMethod.Cmup;
    public ApplicationTheme VisualTheme { get; set; } = ApplicationTheme.LigComMidnight;
    public bool IsActive { get; set; } = true;

    public ICollection<TenantSubscription> Subscriptions { get; set; } = new List<TenantSubscription>();
}
