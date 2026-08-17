using System.Xml.Linq;

namespace Banguat.ExchangeRates.Soap;

internal static class BanguatSoapNamespaces
{
    public static readonly XNamespace Soap =
        "http://schemas.xmlsoap.org/soap/envelope/";

    public static readonly XNamespace Service =
        "http://www.banguat.gob.gt/variables/ws/";

    public const string SoapActionBase =
        "http://www.banguat.gob.gt/variables/ws/";
}