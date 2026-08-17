using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetCurrentUsdRateTextTests
{
    [Fact]
    public async Task Handle_Should_ReturnRawText_OnSuccess()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioDiaStringResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioDiaStringResult>&lt;InfoVariable&gt;&lt;CambioDolar&gt;&lt;VarDolar&gt;&lt;fecha&gt;17/08/2026&lt;/fecha&gt;&lt;referencia&gt;7.61992&lt;/referencia&gt;&lt;/VarDolar&gt;&lt;/CambioDolar&gt;&lt;/InfoVariable&gt;</TipoCambioDiaStringResult></TipoCambioDiaStringResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrentUsdRateText.Handler(transport);

        Result<GetCurrentUsdRateText.Response> result = await handler.Handle(new GetCurrentUsdRateText.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("7.61992", result.Value.Text);
        Assert.Equal("TipoCambioDiaString", transport.LastOperationName);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnexpectedShape_WhenResultMissing()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioDiaStringResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrentUsdRateText.Handler(transport);

        Result<GetCurrentUsdRateText.Response> result = await handler.Handle(new GetCurrentUsdRateText.Query(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        var transport = new FakeBanguatSoapTransport(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        var handler = new GetCurrentUsdRateText.Handler(transport);

        Result<GetCurrentUsdRateText.Response> result = await handler.Handle(new GetCurrentUsdRateText.Query(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }
}
