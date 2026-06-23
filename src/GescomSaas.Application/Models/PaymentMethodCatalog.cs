using System.Text.Json;
using GescomSaas.Domain.Enums;

namespace GescomSaas.Application.Models;

public sealed record PaymentMethodOptionDefinition(PaymentMethod Method, string Label);

public static class PaymentMethodCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<PaymentMethodOptionDefinition> All { get; } =
    [
        new(PaymentMethod.BankTransfer, "Virement"),
        new(PaymentMethod.Cash, "Especes"),
        new(PaymentMethod.Check, "Cheque"),
        new(PaymentMethod.Card, "Carte"),
        new(PaymentMethod.MobileMoney, "Mobile Money"),
        new(PaymentMethod.Other, "Autre")
    ];

    public static IReadOnlyList<PaymentMethod> DefaultSelection { get; } =
    [
        PaymentMethod.BankTransfer,
        PaymentMethod.Cash,
        PaymentMethod.Check
    ];

    public static string GetLabel(PaymentMethod method) =>
        All.FirstOrDefault(x => x.Method == method)?.Label ?? method.ToString();

    public static IReadOnlyList<PaymentMethod> DeserializeSelection(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return DefaultSelection;
        }

        try
        {
            var rawValues = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
            if (rawValues is null || rawValues.Count == 0)
            {
                return DefaultSelection;
            }

            var methods = rawValues
                .Select(ParsePaymentMethod)
                .Where(static x => x.HasValue)
                .Select(static x => x.GetValueOrDefault())
                .Distinct()
                .ToList();

            return methods.Count == 0 ? DefaultSelection : methods;
        }
        catch (JsonException)
        {
            return DefaultSelection;
        }
    }

    public static string SerializeSelection(IEnumerable<PaymentMethod> methods)
    {
        var selected = methods
            .Where(x => Enum.IsDefined(x))
            .Distinct()
            .Select(x => x.ToString())
            .ToList();

        if (selected.Count == 0)
        {
            selected = DefaultSelection.Select(x => x.ToString()).ToList();
        }

        return JsonSerializer.Serialize(selected, JsonOptions);
    }

    private static PaymentMethod? ParsePaymentMethod(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return Enum.TryParse<PaymentMethod>(rawValue, ignoreCase: true, out var method) && Enum.IsDefined(method)
            ? method
            : null;
    }
}
