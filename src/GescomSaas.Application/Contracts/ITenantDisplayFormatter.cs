using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface ITenantDisplayFormatter
{
    TenantDisplaySettings GetSettings();
    string Money(decimal amount, string? currencyCode = null);
    string MoneyEditor(decimal amount);
    string Quantity(decimal quantity, string? unitOfMeasure = null);
    string QuantityEditor(decimal quantity);
    string Number(decimal value, int decimals);
    string RateEditor(decimal value, int decimals = 2);
    string MoneyInputStep();
    string QuantityInputStep();
    string RateInputStep(int decimals = 2);
}
