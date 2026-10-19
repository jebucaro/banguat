using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetCurrencyRateHistorySinceTests
{
    // 2026-09-23T01:00Z is 19:00 on 2026-09-22 in Guatemala: the UTC date has already rolled over while
    // the Guatemala day still has five hours left. Pinning this instant keeps the expected values explicit.
    private static readonly IRateCalendar Calendar =
        new GuatemalaRateCalendar(new FixedTimeProvider(new DateTimeOffset(2026, 9, 23, 1, 0, 0, TimeSpan.Zero)));
    [Fact]
    public async Task Handle_Should_ReturnRatePoints_OnSuccess()
    {
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioFechaInicialMonedaResult><Vars><Var><moneda>18</moneda><fecha>17/08/2026</fecha><venta>17.0271</venta><compra>17.0241</compra></Var></Vars><TotalItems>1</TotalItems></TipoCambioFechaInicialMonedaResult></TipoCambioFechaInicialMonedaResponse></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetCurrencyRateHistorySince.Handler handler = new(transport);

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
        XDocument document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialMonedaResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        FakeBanguatSoapTransport transport = new(Result.Success(document));
        GetCurrencyRateHistorySince.Handler handler = new(transport);

        Result<GetCurrencyRateHistorySince.Response> result = await handler.Handle(
            new GetCurrencyRateHistorySince.Query(new DateOnly(2026, 8, 17), new CurrencyCode(18)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        FakeBanguatSoapTransport transport = new(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        GetCurrencyRateHistorySince.Handler handler = new(transport);

        Result<GetCurrencyRateHistorySince.Response> result = await handler.Handle(
            new GetCurrencyRateHistorySince.Query(new DateOnly(2026, 8, 17), new CurrencyCode(18)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public void GetCacheDuration_Should_ExpireAtNextGuatemalaDay()
    {
        GetCurrencyRateHistorySince.Query query = new(new DateOnly(2020, 1, 1), new CurrencyCode(24));

        Assert.Equal(TimeSpan.FromHours(5), query.GetCacheDuration(Calendar));
    }
}