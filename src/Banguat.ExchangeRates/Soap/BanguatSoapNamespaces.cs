using System.Xml.Linq;

namespace Banguat.ExchangeRates.Soap;

internal static class BanguatSoapNamespaces
{
    public static readonly XNamespace Soap =
        "http://schemas.xmlsoap.org/soap/envelope/";

    public static readonly XNamespace Service =
        "http://www.banguat.gob.gt/variables/ws/"; // NOSONAR: SOAP identifier Banguat matches literally, not a transport URL

    public const string SoapActionBase =
        "http://www.banguat.gob.gt/variables/ws/"; // NOSONAR: SOAP identifier Banguat matches literally, not a transport URL
}