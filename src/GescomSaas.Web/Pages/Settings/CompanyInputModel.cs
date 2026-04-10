using System.ComponentModel.DataAnnotations;
using GescomSaas.Domain.Entities.SaaS;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Web.Pages.Settings;

public class CompanyInputModel
{
    [Required]
    [Display(Name = "Nom commercial")]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Raison sociale")]
    public string CompanyLegalName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "E-mail principal")]
    public string PrimaryContactEmail { get; set; } = string.Empty;

    [Display(Name = "Telephone")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Display(Name = "Adresse ligne 1")]
    public string AddressLine1 { get; set; } = string.Empty;

    [Display(Name = "Adresse ligne 2")]
    public string AddressLine2 { get; set; } = string.Empty;

    [Display(Name = "Code postal")]
    public string PostalCode { get; set; } = string.Empty;

    [Display(Name = "Ville")]
    public string City { get; set; } = string.Empty;

    [Display(Name = "Province / Etat")]
    public string State { get; set; } = string.Empty;

    [Required]
    [StringLength(2)]
    [Display(Name = "Pays")]
    public string CountryCode { get; set; } = "CA";

    [Required]
    [StringLength(3)]
    [Display(Name = "Devise societe")]
    public string CurrencyCode { get; set; } = "CAD";

    [Required]
    [StringLength(3)]
    [Display(Name = "Devise de caisse")]
    public string CashCurrencyCode { get; set; } = "CAD";

    [Required]
    [StringLength(8)]
    [Display(Name = "Symbole devise")]
    public string CurrencySymbol { get; set; } = "$";

    [Display(Name = "Position du symbole")]
    public CurrencySymbolPosition CurrencySymbolPosition { get; set; } = CurrencySymbolPosition.BeforeAmount;

    [Required]
    [Display(Name = "Separateur decimal monnaie")]
    public string MoneyDecimalSeparator { get; set; } = ",";

    [Required]
    [Display(Name = "Separateur de milliers monnaie")]
    public string MoneyGroupSeparator { get; set; } = " ";

    [Range(0, 6)]
    [Display(Name = "Decimales monnaie")]
    public int MoneyDecimalPlaces { get; set; } = 2;

    [Required]
    [Display(Name = "Separateur decimal quantites")]
    public string QuantityDecimalSeparator { get; set; } = ",";

    [Required]
    [Display(Name = "Separateur de milliers quantites")]
    public string QuantityGroupSeparator { get; set; } = " ";

    [Range(0, 6)]
    [Display(Name = "Decimales quantites")]
    public int QuantityDecimalPlaces { get; set; } = 3;

    [Display(Name = "Autoriser le stock negatif")]
    public bool AllowNegativeStock { get; set; }

    [Display(Name = "Suivi de stock par defaut")]
    public StockValuationMethod DefaultStockValuationMethod { get; set; } = StockValuationMethod.Cmup;

    [Display(Name = "Gabarit d'interface")]
    public ApplicationTheme VisualTheme { get; set; } = ApplicationTheme.LigComMidnight;

    public static CompanyInputModel FromEntity(Tenant tenant) =>
        new()
        {
            CompanyName = tenant.CompanyName,
            CompanyLegalName = tenant.CompanyLegalName,
            PrimaryContactEmail = tenant.PrimaryContactEmail,
            PhoneNumber = tenant.PhoneNumber,
            AddressLine1 = tenant.AddressLine1,
            AddressLine2 = tenant.AddressLine2,
            PostalCode = tenant.PostalCode,
            City = tenant.City,
            State = tenant.State,
            CountryCode = tenant.CountryCode,
            CurrencyCode = tenant.CurrencyCode,
            CashCurrencyCode = tenant.CashCurrencyCode,
            CurrencySymbol = tenant.CurrencySymbol,
            CurrencySymbolPosition = tenant.CurrencySymbolPosition,
            MoneyDecimalSeparator = ToSelectorValue(tenant.MoneyDecimalSeparator),
            MoneyGroupSeparator = ToSelectorValue(tenant.MoneyGroupSeparator),
            MoneyDecimalPlaces = tenant.MoneyDecimalPlaces,
            QuantityDecimalSeparator = ToSelectorValue(tenant.QuantityDecimalSeparator),
            QuantityGroupSeparator = ToSelectorValue(tenant.QuantityGroupSeparator),
            QuantityDecimalPlaces = tenant.QuantityDecimalPlaces,
            AllowNegativeStock = tenant.AllowNegativeStock,
            DefaultStockValuationMethod = tenant.DefaultStockValuationMethod,
            VisualTheme = tenant.VisualTheme
        };

    public void ApplyTo(Tenant tenant)
    {
        tenant.CompanyName = CompanyName.Trim();
        tenant.CompanyLegalName = CompanyLegalName.Trim();
        tenant.PrimaryContactEmail = PrimaryContactEmail.Trim();
        tenant.PhoneNumber = PhoneNumber.Trim();
        tenant.AddressLine1 = AddressLine1.Trim();
        tenant.AddressLine2 = AddressLine2.Trim();
        tenant.PostalCode = PostalCode.Trim();
        tenant.City = City.Trim();
        tenant.State = State.Trim();
        tenant.CountryCode = CountryCode.Trim().ToUpperInvariant();
        tenant.CurrencyCode = CurrencyCode.Trim().ToUpperInvariant();
        tenant.CashCurrencyCode = CashCurrencyCode.Trim().ToUpperInvariant();
        tenant.CurrencySymbol = CurrencySymbol.Trim();
        tenant.CurrencySymbolPosition = CurrencySymbolPosition;
        tenant.MoneyDecimalSeparator = NormalizeSeparator(MoneyDecimalSeparator);
        tenant.MoneyGroupSeparator = NormalizeSeparator(MoneyGroupSeparator);
        tenant.MoneyDecimalPlaces = MoneyDecimalPlaces;
        tenant.QuantityDecimalSeparator = NormalizeSeparator(QuantityDecimalSeparator);
        tenant.QuantityGroupSeparator = NormalizeSeparator(QuantityGroupSeparator);
        tenant.QuantityDecimalPlaces = QuantityDecimalPlaces;
        tenant.AllowNegativeStock = AllowNegativeStock;
        tenant.DefaultStockValuationMethod = DefaultStockValuationMethod;
        tenant.VisualTheme = VisualTheme;
    }

    private static string NormalizeSeparator(string value) => value switch
    {
        "space" => " ",
        "none" => string.Empty,
        _ => value.Trim()
    };

    private static string ToSelectorValue(string value) => value switch
    {
        " " => "space",
        "" => "none",
        _ => value
    };
}
