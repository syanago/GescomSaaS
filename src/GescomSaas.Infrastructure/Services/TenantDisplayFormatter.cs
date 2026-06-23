using System.Globalization;
using GescomSaas.Application.Contracts;
using GescomSaas.Application.Models;
using GescomSaas.Domain.Enums;
using GescomSaas.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GescomSaas.Infrastructure.Services;

public class TenantDisplayFormatter(
    ApplicationDbContext dbContext,
    ICurrentTenantAccessor currentTenantAccessor) : ITenantDisplayFormatter
{
    private TenantDisplaySettings? settings;

    public TenantDisplaySettings GetSettings()
    {
        if (settings is not null)
        {
            return settings;
        }

        try
        {
            var tenantId = currentTenantAccessor.GetTenantId();
            var query = dbContext.Tenants.AsNoTracking();
            settings = (tenantId.HasValue
                    ? query.Where(x => x.Id == tenantId.Value)
                    : query.OrderBy(x => x.CompanyName))
                .Select(x => new TenantDisplaySettings(
                    x.CompanyName,
                    x.CompanyLegalName,
                    x.PrimaryContactEmail,
                    x.PhoneNumber,
                    x.AddressLine1,
                    x.AddressLine2,
                    x.PostalCode,
                    x.City,
                    x.State,
                    x.CountryCode,
                    x.CurrencyCode,
                    x.CashCurrencyCode,
                    x.CurrencySymbol,
                    x.CurrencySymbolPosition,
                    x.MoneyDecimalSeparator,
                    x.MoneyGroupSeparator,
                    x.MoneyDecimalPlaces,
                    x.QuantityDecimalSeparator,
                    x.QuantityGroupSeparator,
                    x.QuantityDecimalPlaces,
                    x.VisualTheme))
                .FirstOrDefault()
                ?? TenantDisplaySettings.Default;
        }
        catch (SqlException)
        {
            settings = TenantDisplaySettings.Default;
        }

        return settings;
    }

    public string Money(decimal amount, string? currencyCode = null)
    {
        var current = GetSettings();
        var number = FormatValue(Math.Abs(amount), current.MoneyDecimalPlaces, current.MoneyDecimalSeparator, current.MoneyGroupSeparator);
        var sign = amount < 0 ? "-" : string.Empty;
        var code = string.IsNullOrWhiteSpace(currencyCode) ? current.CurrencyCode : currencyCode.Trim().ToUpperInvariant();
        var isTenantCurrency = string.Equals(code, current.CurrencyCode, StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, current.CashCurrencyCode, StringComparison.OrdinalIgnoreCase);

        if (!isTenantCurrency)
        {
            return $"{sign}{number} {code}";
        }

        var symbol = string.IsNullOrWhiteSpace(current.CurrencySymbol) ? current.CurrencyCode : current.CurrencySymbol.Trim();

        return current.CurrencySymbolPosition == CurrencySymbolPosition.BeforeAmount
            ? $"{sign}{symbol}{number}"
            : $"{sign}{number} {symbol}";
    }

    public string MoneyEditor(decimal amount)
    {
        var current = GetSettings();
        return FormatValue(amount, current.MoneyDecimalPlaces, current.MoneyDecimalSeparator, current.MoneyGroupSeparator);
    }

    public string Quantity(decimal quantity, string? unitOfMeasure = null)
    {
        var current = GetSettings();
        var formatted = FormatValue(quantity, current.QuantityDecimalPlaces, current.QuantityDecimalSeparator, current.QuantityGroupSeparator);
        return string.IsNullOrWhiteSpace(unitOfMeasure) ? formatted : $"{formatted} {unitOfMeasure}";
    }

    public string QuantityEditor(decimal quantity)
    {
        var current = GetSettings();
        return FormatValue(quantity, current.QuantityDecimalPlaces, current.QuantityDecimalSeparator, current.QuantityGroupSeparator);
    }

    public string Number(decimal value, int decimals)
    {
        var current = GetSettings();
        return FormatValue(value, decimals, current.QuantityDecimalSeparator, current.QuantityGroupSeparator);
    }

    public string RateEditor(decimal value, int decimals = 2)
    {
        var current = GetSettings();
        return FormatValue(value, decimals, current.QuantityDecimalSeparator, current.QuantityGroupSeparator);
    }

    public string MoneyInputStep() => BuildStep(GetSettings().MoneyDecimalPlaces);

    public string QuantityInputStep() => BuildStep(GetSettings().QuantityDecimalPlaces);

    public string RateInputStep(int decimals = 2) => BuildStep(decimals);

    private static string FormatValue(decimal value, int decimals, string decimalSeparator, string groupSeparator)
    {
        var format = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
        format.NumberDecimalDigits = Math.Clamp(decimals, 0, 6);
        format.NumberDecimalSeparator = NormalizeDecimalSeparator(decimalSeparator, ",");
        format.NumberGroupSeparator = NormalizeGroupSeparator(groupSeparator, " ");
        format.NumberGroupSizes = [3];
        return value.ToString("N", format);
    }

    private static string NormalizeDecimalSeparator(string? separator, string fallback) =>
        string.IsNullOrEmpty(separator) ? fallback : separator;

    private static string NormalizeGroupSeparator(string? separator, string fallback) =>
        separator is null ? fallback : separator;

    private static string BuildStep(int decimals)
    {
        var safeDecimals = Math.Clamp(decimals, 0, 6);
        return safeDecimals == 0 ? "1" : $"0.{new string('0', safeDecimals - 1)}1";
    }
}
