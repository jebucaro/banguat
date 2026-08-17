using System.Globalization;
using System.Xml.Linq;

namespace Banguat.ExchangeRates.Soap.Models;

internal static class SoapXmlParsing
{
    internal static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly result)
            ? result
            : null;

    internal static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result)
            ? result
            : null;

    internal static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : null;

    internal static IReadOnlyList<T> ParseList<T>(XElement? container, XName itemName, Func<XElement, T?> selector)
    {
        if (container is null)
        {
            return [];
        }

        return container.Elements(itemName)
            .Select(selector)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();
    }
}
