using System.Xml.Linq;
using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Soap;

internal interface IBanguatSoapTransport
{
    Task<Result<XDocument>> InvokeAsync(
        string operationName,
        XElement operation,
        CancellationToken cancellationToken = default);
}