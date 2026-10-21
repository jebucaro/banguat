using System.Globalization;
using System.Xml.Linq;

namespace Banguat.ExchangeRates.Soap.Models;

internal static class SoapXmlParsing
{
    internal static DateOnly? ParseDate(string? value)
    {
        return DateOnly.TryParseExact(value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None,
            out DateOnly result)
            ? result
            : null;
    }

    internal static decimal? ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal result)
            ? result
            : null;
    }

    internal static int? ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : null;
    }

    /// <summary>
    /// Parses every <paramref name="itemName"/> child of <paramref name="container"/>, returning <c>null</c> if any
    /// of them fails to parse so a caller never mistakes a partial list for a complete one. A missing container
    /// yields an empty list; <see cref="CheckTotalItems"/> catches a container that should have held items.
    /// </summary>
    internal static IReadOnlyList<T>? ParseList<T>(XElement? container, XName itemName, Func<XElement, T?> selector)
    {
        if (container is null)
        {
            return [];
        }

        List<T> items = [];
        foreach (XElement element in container.Elements(itemName))
        {
            T? item = selector(element);
            if (item is null)
            {
                return null;
            }

            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// True when the result element's &lt;TotalItems&gt; is absent or matches <paramref name="parsedCount"/>.
    /// </summary>
    internal static bool CheckTotalItems(XElement resultElement, int parsedCount)
    {
        XElement? totalItems = resultElement.Element(BanguatSoapNamespaces.Service + "TotalItems");
        return totalItems is null || ParseInt(totalItems.Value) == parsedCount;
    }
}