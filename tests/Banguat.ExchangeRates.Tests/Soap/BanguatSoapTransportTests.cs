using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Tests.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Banguat.ExchangeRates.Tests.Soap;

[Collection(ActivityListenerCollection.Name)]
public class BanguatSoapTransportTests
{
    private static readonly Uri BaseAddress = new("https://banguat.example/TipoCambio.asmx");

    [Fact]
    public async Task InvokeAsync_Should_ReturnDocumentOnSuccess()
    {
        const string responseBody =
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioDiaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioDiaResult><CambioDolar><VarDolar><fecha>17/08/2026</fecha><referencia>7.61992</referencia></VarDolar></CambioDolar><TotalItems>1</TotalItems></TipoCambioDiaResult></TipoCambioDiaResponse></soap:Body></soap:Envelope>""";

        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody)
        });
        using HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
        BanguatSoapTransport transport = new(httpClient, NullLogger<BanguatSoapTransport>.Instance);

        XElement operation = new(BanguatSoapNamespaces.Service + "TipoCambioDia");

        Result<XDocument> result = await transport.InvokeAsync("TipoCambioDia", operation);

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

        StubHttpMessageHandler handler = new(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(faultBody) });
        using HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
        BanguatSoapTransport transport = new(httpClient, NullLogger<BanguatSoapTransport>.Instance);

        XElement operation = new(BanguatSoapNamespaces.Service + "TipoCambioRango");

        Result<XDocument> result = await transport.InvokeAsync("TipoCambioRango", operation);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.SoapFault", result.Error.Code);
        Assert.Equal(ErrorType.Problem, result.Error.Type);
        Assert.Equal(BanguatErrors.SoapFault().Description, result.Error.Description);
        Assert.DoesNotContain("soap:Server", result.Error.Description);
        Assert.DoesNotContain("unable to process", result.Error.Description);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTransportFailureOnHttpError()
    {
        StubHttpMessageHandler handler = new(_ => throw new HttpRequestException("connection reset"));
        using HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
        BanguatSoapTransport transport = new(httpClient, NullLogger<BanguatSoapTransport>.Instance);

        XElement operation = new(BanguatSoapNamespaces.Service + "TipoCambioDia");

        Result<XDocument> result = await transport.InvokeAsync("TipoCambioDia", operation);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
        Assert.DoesNotContain("connection reset", result.Error.Description);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTransportFailureOnMalformedXml()
    {
        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not xml")
        });
        using HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
        BanguatSoapTransport transport = new(httpClient, NullLogger<BanguatSoapTransport>.Instance);

        XElement operation = new(BanguatSoapNamespaces.Service + "TipoCambioDia");

        Result<XDocument> result = await transport.InvokeAsync("TipoCambioDia", operation);

        Assert.True(result.IsFailure);
        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_Should_RecordSuccessActivity()
    {
        const string responseBody =
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><TipoCambioDiaResponse xmlns="http://www.banguat.gob.gt/variables/ws/"><TipoCambioDiaResult><CambioDolar><VarDolar><fecha>17/08/2026</fecha><referencia>7.61992</referencia></VarDolar></CambioDolar><TotalItems>1</TotalItems></TipoCambioDiaResult></TipoCambioDiaResponse></soap:Body></soap:Envelope>""";

        List<Activity> activities = new();
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == BanguatExchangeRatesDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        StubHttpMessageHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody)
        });
        using HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
        BanguatSoapTransport transport = new(httpClient, NullLogger<BanguatSoapTransport>.Instance);

        XElement operation = new(BanguatSoapNamespaces.Service + "TipoCambioDia");

        Result<XDocument> result = await transport.InvokeAsync("TipoCambioDia", operation);

        Assert.True(result.IsSuccess);
        Activity activity = Assert.Single(activities);
        Assert.Equal("Banguat.ExchangeRates.Soap.TipoCambioDia", activity.DisplayName);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Equal("TipoCambioDia", activity.GetTagItem("banguat.soap.operation"));
        Assert.Equal(200, activity.GetTagItem("http.response.status_code"));
    }

    [Fact]
    public async Task InvokeAsync_Should_RecordFaultActivityWithFaultCodeTag()
    {
        const string faultBody =
            """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Server was unable to process request.</faultstring><detail /></soap:Fault></soap:Body></soap:Envelope>""";

        List<Activity> activities = new();
        using ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == BanguatExchangeRatesDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activities.Add
        };
        ActivitySource.AddActivityListener(listener);

        StubHttpMessageHandler handler = new(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(faultBody) });
        using HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
        BanguatSoapTransport transport = new(httpClient, NullLogger<BanguatSoapTransport>.Instance);

        XElement operation = new(BanguatSoapNamespaces.Service + "TipoCambioRango");

        Result<XDocument> result = await transport.InvokeAsync("TipoCambioRango", operation);

        Assert.True(result.IsFailure);
        Activity activity = Assert.Single(activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("soap:Server", activity.GetTagItem("banguat.soap.fault_code"));
    }

    private static async Task<Result<XDocument>> InvokeWithAsync(
        Func<HttpRequestMessage, HttpResponseMessage> respond, CancellationToken cancellationToken = default)
    {
        StubHttpMessageHandler handler = new(respond);
        using HttpClient httpClient = new(handler) { BaseAddress = BaseAddress };
        BanguatSoapTransport transport = new(httpClient, NullLogger<BanguatSoapTransport>.Instance);

        return await transport.InvokeAsync(
            "TipoCambioDia", new XElement(BanguatSoapNamespaces.Service + "TipoCambioDia"), cancellationToken);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTimeout_WhenPipelineRejectsWithTimeout()
    {
        Result<XDocument> result = await InvokeWithAsync(_ => throw new TimeoutRejectedException("attempt timed out"));

        Assert.Equal("Banguat.Timeout", result.Error.Code);
        Assert.DoesNotContain("attempt timed out", result.Error.Description);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTimeout_WhenHttpClientTimesOut()
    {
        Result<XDocument> result = await InvokeWithAsync(_ =>
            throw new TaskCanceledException("client timeout", new TimeoutException("client timeout")));

        Assert.Equal("Banguat.Timeout", result.Error.Code);
        Assert.DoesNotContain("client timeout", result.Error.Description);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnUnavailable_WhenCircuitIsOpen()
    {
        Result<XDocument> result = await InvokeWithAsync(_ => throw new BrokenCircuitException("circuit open"));

        Assert.Equal("Banguat.Unavailable", result.Error.Code);
        Assert.DoesNotContain("circuit open", result.Error.Description);
    }

    [Fact]
    public async Task InvokeAsync_Should_PropagateCallerCancellation()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            InvokeWithAsync(_ => new HttpResponseMessage(HttpStatusCode.OK), cts.Token));
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTransportFailure_WhenStatusIsErrorAndBodyIsHtml()
    {
        Result<XDocument> result = await InvokeWithAsync(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("<html><body>Maintenance on www.banguat.gob.gt</body>")
        });

        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
        Assert.DoesNotContain("banguat.gob.gt", result.Error.Description);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTransportFailure_WhenStatusIsErrorAndBodyIsEmpty()
    {
        Result<XDocument> result = await InvokeWithAsync(_ =>
            new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent(string.Empty) });

        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTransportFailure_WhenStatusIsErrorAndBodyIsWellFormedNonSoapXml()
    {
        Result<XDocument> result = await InvokeWithAsync(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html><body/></html>")
        });

        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnSoapFault_WhenFaultArrivesWithServerErrorStatus()
    {
        Result<XDocument> result = await InvokeWithAsync(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(
                    """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault><faultcode>soap:Server</faultcode><faultstring>Server was unable to process request.</faultstring><detail /></soap:Fault></soap:Body></soap:Envelope>""")
            });

        Assert.Equal("Banguat.SoapFault", result.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnSoapFault_WhenFaultHasNoCodeOrString()
    {
        Result<XDocument> result = await InvokeWithAsync(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(
                    """<?xml version="1.0" encoding="utf-8"?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><soap:Fault /></soap:Body></soap:Envelope>""")
            });

        Assert.Equal("Banguat.SoapFault", result.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_Should_ReturnTransportFailure_WhenCharsetIsUnknown()
    {
        Result<XDocument> result = await InvokeWithAsync(_ =>
        {
            ByteArrayContent content = new(Encoding.UTF8.GetBytes("<x/>"));
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/xml; charset=bogus");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });

        Assert.Equal("Banguat.TransportFailure", result.Error.Code);
    }

    [Fact]
    public async Task InvokeAsync_Should_LetProgrammingErrorsFromSendPropagate()
    {
        // Thrown by HttpClient for a bad request setup (e.g. no BaseAddress): a bug, not an upstream condition,
        // so it must reach the host's 500 backstop rather than be reported as "could not be reached".
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeWithAsync(_ => throw new InvalidOperationException("invalid request URI")));
    }
}