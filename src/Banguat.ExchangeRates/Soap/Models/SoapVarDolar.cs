using System.Xml.Linq;

namespace Banguat.ExchangeRates.Soap.Models;

internal sealed record SoapVarDolar(DateOnly Fecha, decimal Referencia)
{
    internal static SoapVarDolar? FromElement(XElement element)
    {
        var fecha = SoapXmlParsing.ParseDate(element.Element(BanguatSoapNamespaces.Service + "fecha")?.Value);
        var referencia =
            SoapXmlParsing.ParseDecimal(element.Element(BanguatSoapNamespaces.Service + "referencia")?.Value);

        if (fecha is null || referencia is null) return null;

        return new SoapVarDolar(fecha.Value, referencia.Value);
    }
}