using System.Net;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Tests.Soap;

public class BanguatSoapTransportTests
{
    private static readonly Uri BaseAddress = new("https://banguat.example/TipoCambio.asmx");

    [Fact]
    public async Task InvokeAsync_Should_ReturnDocumentOnSuccess()
    {
        const string responseBody =
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioDiaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioDiaResult><CambioDolar><VarDolar><fecha>17/08/2026</fecha><referencia>7.61992</referencia></VarDolar></CambioDolar><TotalItems>1</TotalItems></TipoCambioDiaResult></TipoCambioDiaResponse></soap:Body></soap:Envelope>""";

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody)
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = BaseAddress };
        var transport = new BanguatSoapTransport(httpClient);

        var operation = new XElement(BanguatSoapNamespaces.Service + "TipoCambioDia");

        var result = await transport.InvokeAsync("TipoCambioDia", operation);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value.Descendants(BanguatSoapNamespaces.Service + "TipoCambioDiaResult")
            .FirstOrDefault());
        Assert.Equal(
            "http://www.banguat.gob.gt/variables/ws/TipoCambioDia",
            handler.LastRequest!.Headers.GetValues("SOAPAction").Single().Trim('"'));
        Assert.Contains("<TipoCambioDia", handler.LastRequestBody);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnSoapFaultFailure()
    {
        const string faultBody =
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Server was unable to process request.</faultstring><detail /></soap:Fault></soap:Body></soap:Envelope>""";

        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(faultBody)
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = BaseAddress };
        var transport = new BanguatSoapTransport(httpClient);

        var operation = new XElement(BanguatSoapNamespaces.Service + "TipoCambioRango");

        var result = await transport.InvokeAsync("TipoCambioRango", operation);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.SoapFault", result.Error.Code);
        Assert.Equal(ErrorType.Problem, result.Error.Type);
        Assert.Contains("soap:Server", result.Error.Description);
        Assert.Contains("unable to process", result.Error.Description);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTransportFailureOnHttpError()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("connection reset"));
        using var httpClient = new HttpClient(handler) { BaseAddress = BaseAddress };
        var transport = new BanguatSoapTransport(httpClient);

        var operation = new XElement(BanguatSoapNamespaces.Service + "TipoCambioDia");

        var result = await transport.InvokeAsync("TipoCambioDia", operation);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
        Assert.Contains("connection reset", result.Error.Description);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTransportFailureOnMalformedXml()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not xml")
        });
        using var httpClient = new HttpClient(handler) { BaseAddress = BaseAddress };
        var transport = new BanguatSoapTransport(httpClient);

        var operation = new XElement(BanguatSoapNamespaces.Service + "TipoCambioDia");

        var result = await transport.InvokeAsync("TipoCambioDia", operation);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }
}