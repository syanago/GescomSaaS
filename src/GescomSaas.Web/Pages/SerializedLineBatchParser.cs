using System.Text.RegularExpressions;

namespace GescomSaas.Web.Pages;

internal static partial class SerializedLineBatchParser
{
    public static IReadOnlyList<string> Parse(
        string? mode,
        string? singleSerial,
        string? serialList,
        string? rangeStart,
        string? rangeEnd)
    {
        return NormalizeMode(mode) switch
        {
            SerialEntryMode.Enumeration => ParseEnumeration(serialList),
            SerialEntryMode.Range => ParseRange(rangeStart, rangeEnd),
            _ => ParseSingle(singleSerial)
        };
    }

    private static IReadOnlyList<string> ParseSingle(string? singleSerial)
    {
        var normalized = NormalizeToken(singleSerial);
        return string.IsNullOrWhiteSpace(normalized) ? [] : [normalized];
    }

    private static IReadOnlyList<string> ParseEnumeration(string? serialList)
    {
        if (string.IsNullOrWhiteSpace(serialList))
        {
            return [];
        }

        var values = serialList
            .Split([',', ';', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeToken)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return values;
    }

    private static IReadOnlyList<string> ParseRange(string? rangeStart, string? rangeEnd)
    {
        var start = NormalizeToken(rangeStart);
        var end = NormalizeToken(rangeEnd);
        if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
        {
            return [];
        }

        var startMatch = SerialRangePattern().Match(start);
        var endMatch = SerialRangePattern().Match(end);
        if (!startMatch.Success || !endMatch.Success)
        {
            throw new InvalidOperationException("La plage de numeros de serie doit se terminer par une partie numerique.");
        }

        var startPrefix = startMatch.Groups["prefix"].Value;
        var endPrefix = endMatch.Groups["prefix"].Value;
        var startDigits = startMatch.Groups["digits"].Value;
        var endDigits = endMatch.Groups["digits"].Value;

        if (!string.Equals(startPrefix, endPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Le debut et la fin de plage doivent partager le meme prefixe.");
        }

        if (startDigits.Length != endDigits.Length)
        {
            throw new InvalidOperationException("Le debut et la fin de plage doivent avoir la meme longueur numerique.");
        }

        var startValue = int.Parse(startDigits);
        var endValue = int.Parse(endDigits);
        if (endValue < startValue)
        {
            throw new InvalidOperationException("La fin de plage doit etre superieure ou egale au debut.");
        }

        var count = endValue - startValue + 1;
        if (count > 200)
        {
            throw new InvalidOperationException("La plage de numeros de serie est trop grande. Reste sur un petit lot de 200 numeros maximum.");
        }

        return Enumerable.Range(startValue, count)
            .Select(value => $"{startPrefix}{value.ToString().PadLeft(startDigits.Length, '0')}")
            .ToList();
    }

    private static string? NormalizeToken(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToUpperInvariant();

    private static SerialEntryMode NormalizeMode(string? mode) =>
        Enum.TryParse<SerialEntryMode>(mode, true, out var parsed)
            ? parsed
            : SerialEntryMode.Single;

    private enum SerialEntryMode
    {
        Single,
        Enumeration,
        Range
    }

    [GeneratedRegex("^(?<prefix>.*?)(?<digits>\\d+)$", RegexOptions.Compiled)]
    private static partial Regex SerialRangePattern();
}
