using GescomSaas.Application.Models;

namespace GescomSaas.Application.Contracts;

public interface ITenantDisplayFormatter
{
    TenantDisplaySettings GetSettings();
    string Money(decimal amount, string? currencyCode = null);
    string Quantity(decimal quantity, string? unitOfMeasure = null);
    string Number(decimal value, int decimals);
    string MoneyInputStep();
    string QuantityInputStep();
    string RateInputStep(int decimals = 2);
}
