using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetCurrencyRateHistoryTests
{
    [Fact]
    public async Task Handle_Should_ReturnRatePoints_OnSuccess()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioRangoMonedaResult><Vars><Var><moneda>18</moneda><fecha>03/08/2026</fecha><venta>17.2985</venta><compra>17.2953</compra></Var></Vars><TotalItems>1</TotalItems></TipoCambioRangoMonedaResult></TipoCambioRangoMonedaResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrencyRateHistory.Handler(transport);

        Result<GetCurrencyRateHistory.Response> result = await handler.Handle(
            new GetCurrencyRateHistory.Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 3), new CurrencyCode(18)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        GetCurrencyRateHistory.RatePoint point = Assert.Single(result.Value.Rates);
        Assert.Equal(17.2953m, point.Buy);
        Assert.Equal(17.2985m, point.Sell);
        Assert.Equal("TipoCambioRangoMoneda", transport.LastOperationName);
        Assert.Equal(
            "18",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "moneda")!.Value);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidDateRange_WhenToIsBeforeFrom()
    {
        var transport = new FakeBanguatSoapTransport(Result.Success(new XDocument()));
        var handler = new GetCurrencyRateHistory.Handler(transport);

        Result<GetCurrencyRateHistory.Response> result = await handler.Handle(
            new GetCurrencyRateHistory.Query(new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 1), new CurrencyCode(18)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.InvalidDateRange", result.Error.Code);
        Assert.Null(transport.LastOperationName);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnexpectedShape_WhenResultMissing()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrencyRateHistory.Handler(transport);

        Result<GetCurrencyRateHistory.Response> result = await handler.Handle(
            new GetCurrencyRateHistory.Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 3), new CurrencyCode(18)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        var transport = new FakeBanguatSoapTransport(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        var handler = new GetCurrencyRateHistory.Handler(transport);

        Result<GetCurrencyRateHistory.Response> result = await handler.Handle(
            new GetCurrencyRateHistory.Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 3), new CurrencyCode(18)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }
}
