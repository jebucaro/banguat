using System.Diagnostics;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Features;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Tests.Diagnostics;

namespace Banguat.ExchangeRates.Tests.Features;

[Collection(ActivityListenerCollection.Name)]
public class GetCurrentRateTests
{
    [Fact]
    public async Task Handle_Should_ReturnBuySell_ForCambioDiaShape()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><VariablesResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><VariablesResult><CambioDia><Var><moneda>18</moneda><fecha>17/08/2026</fecha><venta>17.0271</venta><compra>17.0241</compra></Var></CambioDia><TotalItems>1</TotalItems></VariablesResult></VariablesResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrentRate.Handler(transport);

        var result = await handler.Handle(
            new GetCurrentRate.Query(new CurrencyCode(18)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var point = Assert.Single(result.Value.Rates);
        Assert.Equal(new DateOnly(2026, 8, 17), point.Date);
        Assert.Equal(17.0241m, point.Buy);
        Assert.Equal(17.0271m, point.Sell);
        Assert.Equal("Variables", transport.LastOperationName);
        Assert.Equal("18", transport.LastOperation!.Element(BanguatSoapNamespaces.Service + "variable")!.Value);
    }

    [Fact]
    public async Task Handle_Should_ReturnEqualBuySell_ForCambioDolarShape()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><VariablesResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><VariablesResult><CambioDolar><VarDolar><fecha>17/08/2026</fecha><referencia>7.61992</referencia></VarDolar></CambioDolar><TotalItems>1</TotalItems></VariablesResult></VariablesResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrentRate.Handler(transport);

        var result = await handler.Handle(
            new GetCurrentRate.Query(new CurrencyCode(2)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var point = Assert.Single(result.Value.Rates);
        Assert.Equal(new DateOnly(2026, 8, 17), point.Date);
        Assert.Equal(7.61992m, point.Buy);
        Assert.Equal(7.61992m, point.Sell);
    }

    [Fact]
    public async Task Handle_Should_ReturnEmptyList_WhenNeitherShapeIsPopulated()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><VariablesResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><VariablesResult><CambioDia /><TotalItems>0</TotalItems></VariablesResult></VariablesResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrentRate.Handler(transport);

        var result = await handler.Handle(
            new GetCurrentRate.Query(new CurrencyCode(9999)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Rates);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        var transport = new FakeBanguatSoapTransport(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        var handler = new GetCurrentRate.Handler(transport);

        var result = await handler.Handle(
            new GetCurrentRate.Query(new CurrencyCode(2)), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_TagCurrentActivityWithCurrency()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><VariablesResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><VariablesResult><CambioDia /><TotalItems>0</TotalItems></VariablesResult></VariablesResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetCurrentRate.Handler(transport);

        using var activitySource = new ActivitySource(BanguatExchangeRatesDiagnostics.ActivitySourceName);
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BanguatExchangeRatesDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("Probe");
        await handler.Handle(new GetCurrentRate.Query(new CurrencyCode(18)), CancellationToken.None);

        Assert.Equal(18, activity!.GetTagItem("banguat.currency"));
    }
}