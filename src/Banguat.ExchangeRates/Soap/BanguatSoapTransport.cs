using System.Net.Http.Headers;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;

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

        var envelope = CreateEnvelope(operation);
        var envelopeXml = envelope.Declaration + envelope.ToString(SaveOptions.DisableFormatting);

        using var request = new HttpRequestMessage(HttpMethod.Post, (Uri?)null)
        {
            Content = new StringContent(envelopeXml, Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml") { CharSet = "utf-8" };
        request.Headers.Add("SOAPAction", $"\"{BanguatSoapNamespaces.SoapActionBase}{operationName}\"");

        string body;
        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            return Result.Failure<XDocument>(BanguatErrors.TransportFailure(ex.Message));
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return Result.Failure<XDocument>(BanguatErrors.TransportFailure(ex.Message));
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(body);
        }
        catch (XmlException ex)
        {
            return Result.Failure<XDocument>(
                BanguatErrors.TransportFailure($"Response body was not valid XML: {ex.Message}"));
        }

        var fault = document.Descendants(BanguatSoapNamespaces.Soap + "Fault").FirstOrDefault();
        if (fault is not null)
        {
            var faultCode = fault.Element("faultcode")?.Value ?? "Unknown";
            var faultString = fault.Element("faultstring")?.Value
                              ?? "The service returned an unspecified SOAP fault.";
            return Result.Failure<XDocument>(BanguatErrors.SoapFault(faultCode, faultString));
        }

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