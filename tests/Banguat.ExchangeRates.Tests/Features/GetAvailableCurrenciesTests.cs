using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;

namespace Banguat.ExchangeRates.Tests.Features;

public class GetAvailableCurrenciesTests
{
    [Fact]
    public async Task Handle_Should_ReturnCatalog_OnSuccess()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><VariablesDisponiblesResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><VariablesDisponiblesResult><Variables><Variable><moneda>1</moneda><descripcion>Quetzales</descripcion></Variable><Variable><moneda>2</moneda><descripcion>Dólares de EE.UU.</descripcion></Variable></Variables><TotalItems>2</TotalItems></VariablesDisponiblesResult></VariablesDisponiblesResponse></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetAvailableCurrencies.Handler(transport);

        var result = await handler.Handle(new GetAvailableCurrencies.Query(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Currencies.Count);
        Assert.Equal(new CurrencyCode(1), result.Value.Currencies[0].Code);
        Assert.Equal("Quetzales", result.Value.Currencies[0].Description);
        Assert.Equal(new CurrencyCode(2), result.Value.Currencies[1].Code);
        Assert.Equal("Dólares de EE.UU.", result.Value.Currencies[1].Description);
        Assert.Equal("VariablesDisponibles", transport.LastOperationName);
    }

    [Fact]
    public async Task Handle_Should_ReturnUnexpectedShape_WhenResultMissing()
    {
        var document = XDocument.Parse(
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><VariablesDisponiblesResponse xmlns="http://www.banguat.gob.gt/variables/ws/" /></soap:Body></soap:Envelope>""");
        var transport = new FakeBanguatSoapTransport(Result.Success(document));
        var handler = new GetAvailableCurrencies.Handler(transport);

        var result = await handler.Handle(new GetAvailableCurrencies.Query(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.UnexpectedResponseShape", result.Error.Code);
    }

    [Fact]
    public async Task Handle_Should_PropagateTransportFailure()
    {
        var transport = new FakeBanguatSoapTransport(
            Result.Failure<XDocument>(BanguatErrors.TransportFailure("timeout")));
        var handler = new GetAvailableCurrencies.Handler(transport);

        var result = await handler.Handle(new GetAvailableCurrencies.Query(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }
}