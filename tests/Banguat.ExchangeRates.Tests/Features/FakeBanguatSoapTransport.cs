using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Tests.Features;

internal sealed class FakeBanguatSoapTransport(Result<XDocument> response) : IBanguatSoapTransport
{
    public string? LastOperationName { get; private set; }

    public XElement? LastOperation { get; private set; }

    public Task<Result<XDocument>> InvokeAsync(
        string operationName, XElement operation, CancellationToken cancellationToken = default)
    {
        LastOperationName = operationName;
        LastOperation = operation;

        return Task.FromResult(response);
    }
}