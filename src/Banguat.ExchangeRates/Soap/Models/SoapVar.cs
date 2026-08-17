using System.Xml.Linq;

namespace Banguat.ExchangeRates.Soap.Models;

internal sealed record SoapVar(DateOnly Fecha, decimal Venta, decimal Compra)
{
    internal static SoapVar? FromElement(XElement element)
    {
        DateOnly? fecha = SoapXmlParsing.ParseDate(element.Element(BanguatSoapNamespaces.Service + "fecha")?.Value);
        decimal? venta = SoapXmlParsing.ParseDecimal(element.Element(BanguatSoapNamespaces.Service + "venta")?.Value);
        decimal? compra = SoapXmlParsing.ParseDecimal(element.Element(BanguatSoapNamespaces.Service + "compra")?.Value);

        if (fecha is null || venta is null || compra is null)
        {
            return null;
        }

        return new SoapVar(fecha.Value, venta.Value, compra.Value);
    }
}
