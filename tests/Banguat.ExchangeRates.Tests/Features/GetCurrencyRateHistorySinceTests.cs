using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetCurrencyRateHistorySinceTests
{
    [Fact]
    public async Task Handle_Should_ReturnRatePoints_OnSuccess()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioFechaInicialMonedaResult><Vars><Var><moneda>18</moneda><fecha>17/08/2026</fecha><venta>17.0271</venta><compra>17.0241</compra></Var></Vars><TotalItems>1</TotalItems></TipoCambioFechaInicialMonedaResult></TipoCambioFechaInicialMonedaResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrencyRateHistorySince.Handler(transport);

        Result<GetCurrencyRateHistorySince.Response> result = await handler.Handle(
            new GetCurrencyRateHistorySince.Query(new DateOnly(2026, 8, 17), new CurrencyCode(18)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        GetCurrencyRateHistorySince.RatePoint point = Assert.Single(result.Value.Rates);
        Assert.Equal(17.0241m, point.Buy);
        Assert.Equal(17.0271m, point.Sell);
        Assert.Equal("TipoCambioFechaInicialMoneda", transport.LastOperationName);
        Assert.Equal(
            "17/08/2026",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "fechainit")!.Value);
        Assert.Equal(
            "18",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "moneda")!.Value);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnexpectedShape_WhenResultMissing()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrencyRateHistorySince.Handler(transport);

        Result<GetCurrencyRateHistorySince.Response> result = await handler.Handle(
            new GetCurrencyRateHistorySince.Query(new DateOnly(2026, 8, 17), new CurrencyCode(18)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        var transport = new FakeBanguatSoapTransport(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        var handler = new GetCurrencyRateHistorySince.Handler(transport);

        Result<GetCurrencyRateHistorySince.Response> result = await handler.Handle(
            new GetCurrencyRateHistorySince.Query(new DateOnly(2026, 8, 17), new CurrencyCode(18)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }
}
