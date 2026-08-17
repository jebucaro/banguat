using System.Xml.Linq;

namespace Banguat.ExchangeRates.Soap.Models;

internal sealed record SoapVariable(int Moneda, string Descripcion)
{
    internal static SoapVariable? FromElement(XElement element)
    {
        var moneda = SoapXmlParsing.ParseInt(element.Element(BanguatSoapNamespaces.Service + "moneda")?.Value);

        if (moneda is null) return null;

        var descripcion = element.Element(BanguatSoapNamespaces.Service + "descripcion")?.Value ?? string.Empty;

        return new SoapVariable(moneda.Value, descripcion);
    }
}