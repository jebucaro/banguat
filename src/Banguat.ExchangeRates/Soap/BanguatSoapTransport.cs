using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Diagnostics;

namespace Banguat.ExchangeRates.Soap;

internal sealed class BanguatSoapTransport(HttpClient httpClient) : IBanguatSoapTransport
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
        try
        {
            HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken);
            activity?.SetTag("http.response.status_code", (int)response.StatusCode);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure<XDocument>(BanguatErrors.TransportFailure(ex.Message));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure<XDocument>(BanguatErrors.TransportFailure(ex.Message));
        }

        activity?.SetTag("banguat.soap.response.bytes", Encoding.UTF8.GetByteCount(body));

        XDocument document;
        try
        {
            document = XDocument.Parse(body);
        }
        catch (XmlException ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return Result.Failure<XDocument>(
                BanguatErrors.TransportFailure($"Response body was not valid XML: {ex.Message}"));
        }

        XElement? fault = document.Descendants(BanguatSoapNamespaces.Soap + "Fault").FirstOrDefault();
        if (fault is not null)
        {
            string faultCode = fault.Element("faultcode")?.Value ?? "Unknown";
            string faultString = fault.Element("faultstring")?.Value
                                 ?? "The service returned an unspecified SOAP fault.";
            activity?.SetTag("banguat.soap.fault_code", faultCode);
            activity?.SetStatus(ActivityStatusCode.Error, faultString);
            return Result.Failure<XDocument>(BanguatErrors.SoapFault(faultCode, faultString));
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return Result.Success(document);
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