using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetUsdRateHistorySinceTests
{
    [Fact]
    public async Task Handle_Should_ReturnRatePoints_OnSuccess()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioFechaInicialResult><Vars><Var><moneda>2</moneda><fecha>16/08/2026</fecha><venta>7.6231</venta><compra>7.6231</compra></Var><Var><moneda>2</moneda><fecha>17/08/2026</fecha><venta>7.61992</venta><compra>7.61992</compra></Var></Vars><TotalItems>2</TotalItems></TipoCambioFechaInicialResult></TipoCambioFechaInicialResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetUsdRateHistorySince.Handler(transport);

        Result<GetUsdRateHistorySince.Response> result = await handler.Handle(
            new GetUsdRateHistorySince.Query(new DateOnly(2026, 8, 16)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Rates.Count);
        Assert.Equal(new DateOnly(2026, 8, 17), result.Value.Rates[1].Date);
        Assert.Equal(7.61992m, result.Value.Rates[1].Buy);
        Assert.Equal("TipoCambioFechaInicial", transport.LastOperationName);
        Assert.Equal(
            "16/08/2026",
            transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "fechainit")!.Value);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyList_WhenVarsIsEmpty()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioFechaInicialResult><Vars /><TotalItems>0</TotalItems></TipoCambioFechaInicialResult></TipoCambioFechaInicialResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetUsdRateHistorySince.Handler(transport);

        Result<GetUsdRateHistorySince.Response> result = await handler.Handle(
            new GetUsdRateHistorySince.Query(new DateOnly(2099, 1, 1)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Rates);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnexpectedShape_WhenResultMissing()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioFechaInicialResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetUsdRateHistorySince.Handler(transport);

        Result<GetUsdRateHistorySince.Response> result = await handler.Handle(
            new GetUsdRateHistorySince.Query(new DateOnly(2026, 8, 16)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        var transport = new FakeBanguatSoapTransport(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        var handler = new GetUsdRateHistorySince.Handler(transport);

        Result<GetUsdRateHistorySince.Response> result = await handler.Handle(
            new GetUsdRateHistorySince.Query(new DateOnly(2026, 8, 16)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }
}
