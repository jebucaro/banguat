using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetCurrentUsdRateTests
{
    private static readonly XDocument SuccessDocument = XDocument.Parse(
        """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioDiaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioDiaResult><CambioDolar><VarDolar><fecha>17/08/2026</fecha><referencia>7.61992</referencia></VarDolar></CambioDolar><TotalItems>1</TotalItems></TipoCambioDiaResult></TipoCambioDiaResponse></soap:Body></soap:Envelope>""");

    [Fact]
    public async Task Handle_Should_ReturnTodaysRate_OnSuccess()
    {
        FakeBanguatSoapTransport transport = new(Result.Success(SuccessDocument));
        GetCurrentUsdRate.Handler handler = new(transport);

        Result<GetCurrentUsdRate.Response> result =
            await handler.Handle(new GetCurrentUsdRate.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2026, 8, 17), result.Value.Date);
        Assert.Equal(7.61992m, result.Value.Rate);
        Assert.Equal("TipoCambioDia", transport.LastOperationName);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        FakeBanguatSoapTransport transport = new(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        GetCurrentUsdRate.Handler handler = new(transport);

        Result<GetCurrentUsdRate.Response> result =
            await handler.Handle(new GetCurrentUsdRate.Query(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnexpectedShape_WhenResultElementMissing()
    {
        XDocument emptyDocument = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioDiaResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(emptyDocument));
        GetCurrentUsdRate.Handler handler = new(transport);

        Result<GetCurrentUsdRate.Response> result =
            await handler.Handle(new GetCurrentUsdRate.Query(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }
}