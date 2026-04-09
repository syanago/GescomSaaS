using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record TenantDisplaySettings(
    string CompanyName,
    string CompanyLegalName,
    string PrimaryContactEmail,
    string PhoneNumber,
    string AddressLine1,
    string AddressLine2,
    string PostalCode,
    string City,
    string State,
    string CountryCode,
    string CurrencyCode,
    string CashCurrencyCode,
    string CurrencySymbol,
    CurrencySymbolPosition CurrencySymbolPosition,
    string MoneyDecimalSeparator,
    string MoneyGroupSeparator,
    int MoneyDecimalPlaces,
    string QuantityDecimalSeparator,
    string QuantityGroupSeparator,
    int QuantityDecimalPlaces,
    ApplicationTheme VisualTheme)
{
    public string ThemeCssClass => VisualTheme switch
    {
        ApplicationTheme.LigComEmeraldLight => "theme-emerald-light",
        _ => "theme-midnight"
    };

    public string ThemeLabel => VisualTheme switch
    {
        ApplicationTheme.LigComEmeraldLight => "LigCom Vert Clair",
        _ => "LigCom Nuit"
    };

    public static TenantDisplaySettings Default { get; } = new(
        "LigCom",
        "LigCom",
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        "CA",
        "CAD",
        "CAD",
        "$",
        CurrencySymbolPosition.BeforeAmount,
        ",",
        " ",
        2,
        ",",
        " ",
        3,
        ApplicationTheme.LigComMidnight);
}
