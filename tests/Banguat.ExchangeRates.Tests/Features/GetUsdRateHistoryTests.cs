using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetUsdRateHistoryTests
{
    [Fact]
    public async Task Handle_Should_ReturnRatePoints_OnSuccess()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioRangoResult><Vars><Var><moneda>2</moneda><fecha>01/08/2026</fecha><venta>7.6350</venta><compra>7.6231</compra></Var><Var><moneda>2</moneda><fecha>05/08/2026</fecha><venta>7.62541</venta><compra>7.62484</compra></Var></Vars><TotalItems>2</TotalItems></TipoCambioRangoResult></TipoCambioRangoResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetUsdRateHistory.Handler(transport);

        Result<GetUsdRateHistory.Response> result = await handler.Handle(
            new GetUsdRateHistory.Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Rates.Count);
        Assert.Equal(new DateOnly(2026, 8, 1), result.Value.Rates[0].Date);
        Assert.Equal(7.6231m, result.Value.Rates[0].Buy);
        Assert.Equal(7.6350m, result.Value.Rates[0].Sell);
        Assert.Equal(new DateOnly(2026, 8, 5), result.Value.Rates[1].Date);
        Assert.Equal(7.62484m, result.Value.Rates[1].Buy);
        Assert.Equal(7.62541m, result.Value.Rates[1].Sell);
        Assert.Equal("TipoCambioRango", transport.LastOperationName);
        Assert.Equal(
            "01/08/2026",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "fechainit")!.Value);
        Assert.Equal(
            "05/08/2026",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "fechafin")!.Value);
    }

    [Fact]
    public async Task Handle_Should_ReturnInvalidDateRange_WhenToIsBeforeFrom()
    {
        var transport = new FakeBanguatSoapTransport(Result.Success(new XDocument()));
        var handler = new GetUsdRateHistory.Handler(transport);

        Result<GetUsdRateHistory.Response> result = await handler.Handle(
            new GetUsdRateHistory.Query(new DateOnly(2026, 8, 5), new DateOnly(2026, 8, 1)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.InvalidDateRange", result.Error.Code);
        Assert.Null(transport.LastOperationName);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnexpectedShape_WhenResultMissing()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioRangoResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetUsdRateHistory.Handler(transport);

        Result<GetUsdRateHistory.Response> result = await handler.Handle(
            new GetUsdRateHistory.Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        var transport = new FakeBanguatSoapTransport(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        var handler = new GetUsdRateHistory.Handler(transport);

        Result<GetUsdRateHistory.Response> result = await handler.Handle(
            new GetUsdRateHistory.Query(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 5)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }
}
