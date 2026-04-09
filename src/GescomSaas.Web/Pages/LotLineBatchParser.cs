namespace GescomSaas.Web.Pages;

internal static class LotLineBatchParser
{
    public static IReadOnlyList<LotBatchEntry> Parse(string? mode, string? singleLot, decimal quantity, string? breakdown)
    {
        return string.Equals(mode, "Breakdown", StringComparison.OrdinalIgnoreCase)
            ? ParseBreakdown(breakdown)
            : ParseSingle(singleLot, quantity);
    }

    private static IReadOnlyList<LotBatchEntry> ParseSingle(string? singleLot, decimal quantity)
    {
        var normalized = NormalizeToken(singleLot);
        return string.IsNullOrWhiteSpace(normalized) ? [] : [new LotBatchEntry(normalized, quantity)];
    }

    private static IReadOnlyList<LotBatchEntry> ParseBreakdown(string? breakdown)
    {
        if (string.IsNullOrWhiteSpace(breakdown))
        {
            return [];
        }

        var result = new List<LotBatchEntry>();
        var lines = breakdown
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var rawLine in lines)
        {
            var parts = rawLine
                .Split([';', '|', '=', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != 2)
            {
                throw new InvalidOperationException("Chaque ligne de repartition lot doit etre au format LOT;QUANTITE.");
            }

            var lot = NormalizeToken(parts[0]);
            if (string.IsNullOrWhiteSpace(lot))
            {
                throw new InvalidOperationException("Chaque ligne de repartition lot doit contenir un numero de lot.");
            }

            if (!TryParseDecimal(parts[1], out var quantity) || quantity <= 0m)
            {
                throw new InvalidOperationException("Chaque ligne de repartition lot doit contenir une quantite strictement positive.");
            }

            result.Add(new LotBatchEntry(lot, quantity));
        }

        return result
            .GroupBy(x => x.LotNumber, StringComparer.OrdinalIgnoreCase)
            .Select(group => new LotBatchEntry(group.First().LotNumber, group.Sum(x => x.Quantity)))
            .ToList();
    }

    private static bool TryParseDecimal(string value, out decimal quantity) =>
        decimal.TryParse(value.Replace(" ", "").Replace(',', '.'), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out quantity);

    private static string? NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();
}

internal sealed record LotBatchEntry(string LotNumber, decimal Quantity);
