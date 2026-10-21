using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Diagnostics;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;

namespace Banguat.ExchangeRates.Soap;

internal sealed class BanguatSoapTransport(HttpClient httpClient, ILogger<BanguatSoapTransport> logger)
    : IBanguatSoapTransport
{
    public async Task<Result<XDocument>> InvokeAsync(
        string operationName,
        XElement operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operation);

        using Activity? activity = BanguatExchangeRatesDiagnostics.ActivitySource.StartActivity(
            $"Banguat.ExchangeRates.Soap.{operationName}", ActivityKind.Client);
        activity?.SetTag("banguat.soap.operation", operationName);
        activity?.SetTag("server.address", httpClient.BaseAddress?.Host);
        activity?.SetTag("http.request.method", "POST");

        XDocument envelope = CreateEnvelope(operation);
        string envelopeXml = envelope.Declaration + envelope.ToString(SaveOptions.DisableFormatting);

        using HttpRequestMessage request = new(HttpMethod.Post, (Uri?)null)
        {
            Content = new StringContent(envelopeXml, Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        request.Headers.Add("SOAPAction", $"\"{BanguatSoapNamespaces.SoapActionBase}{operationName}\"");

        string body;
        HttpStatusCode statusCode;
        HttpResponseMessage? response = null;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
            statusCode = response.StatusCode;
            activity?.SetTag("http.response.status_code", (int)statusCode);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (TimeoutRejectedException ex)
        {
            return Fail(activity, operationName, BanguatErrors.Timeout(), ex.Message, ex);
        }
        catch (ExecutionRejectedException ex)
        {
            // Any other rejection from the resilience pipeline (e.g. an open circuit) means no request was sent.
            return Fail(activity, operationName, BanguatErrors.Unavailable(), ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail(activity, operationName, BanguatErrors.Timeout(), ex.Message, ex);
        }
        catch (HttpRequestException ex)
        {
            return Fail(activity, operationName, BanguatErrors.TransportFailure(), ex.Message, ex);
        }
        catch (InvalidOperationException ex) when (response is not null)
        {
            // ReadAsStringAsync throws this when the response declares a charset it can't decode. Before a response
            // exists it means a bad request setup, which is a bug for the host's error handling, not an upstream state.
            return Fail(activity, operationName, BanguatErrors.TransportFailure(), ex.Message, ex);
        }
        finally
        {
            response?.Dispose();
        }

        activity?.SetTag("banguat.soap.response.bytes", Encoding.UTF8.GetByteCount(body));

        XDocument document;
        try
        {
            document = XDocument.Parse(body);
        }
        catch (XmlException ex)
        {
            return Fail(activity, operationName, BanguatErrors.TransportFailure(),
                $"HTTP {(int)statusCode}: response body was not valid XML: {ex.Message}", ex);
        }

        // A SOAP 1.1 fault normally arrives with HTTP 500, so the fault check comes before the status check.
        XElement? fault = document.Descendants(BanguatSoapNamespaces.Soap + "Fault").FirstOrDefault();
        if (fault is not null)
        {
            string faultCode = fault.Element("faultcode")?.Value ?? "Unknown";
            string faultString = fault.Element("faultstring")?.Value
                                 ?? "The service returned an unspecified SOAP fault.";
            activity?.SetTag("banguat.soap.fault_code", faultCode);
            return Fail(activity, operationName, BanguatErrors.SoapFault(), $"{faultCode}: {faultString}");
        }

        if ((int)statusCode is < 200 or > 299)
        {
            return Fail(activity, operationName, BanguatErrors.TransportFailure(),
                $"HTTP {(int)statusCode} without a SOAP fault");
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return Result.Success(document);
    }

    private Result<XDocument> Fail(
        Activity? activity, string operationName, Error error, string reason, Exception? exception = null)
    {
        activity?.SetStatus(ActivityStatusCode.Error, reason);
        if (exception is not null)
        {
            activity?.AddException(exception);
        }

        logger.LogWarning(exception, "Banguat {Operation} failed: {Reason}", operationName, reason);
        return Result.Failure<XDocument>(error);
    }

    private static XDocument CreateEnvelope(XElement operation)
    {
        return new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(
                BanguatSoapNamespaces.Soap + "Envelope",
                new XAttribute(
                    XNamespace.Xmlns + "soap",
                    BanguatSoapNamespaces.Soap.NamespaceName),
                new XElement(
                    BanguatSoapNamespaces.Soap + "Body",
                    operation)
            ));
    }
}