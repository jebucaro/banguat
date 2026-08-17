using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Features;

public static class GetAvailableCurrencies
{
    public sealed record Query : IQuery<Response>;

    public sealed record CurrencyCatalogEntry(CurrencyCode Code, string Description);

    public sealed record Response(IReadOnlyList<CurrencyCatalogEntry> Currencies);

    internal sealed class Handler(IBanguatSoapTransport transport) : IQueryHandler<Query, Response>
    {
        private const string OperationName = "VariablesDisponibles";

        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var request = new XElement(BanguatSoapNamespaces.Service + OperationName);

            Result<XDocument> transportResult = await transport.InvokeAsync(OperationName, request, cancellationToken);

            return transportResult.IsFailure
                ? Result.Failure<Response>(transportResult.Error)
                : Parser.Parse(transportResult.Value);
        }
    }

    internal static class Parser
    {
        internal static Result<Response> Parse(XDocument document)
        {
            XElement? resultElement = document
                .Descendants(BanguatSoapNamespaces.Service + "VariablesDisponiblesResult")
                .FirstOrDefault();

            if (resultElement is null)
            {
                return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape("VariablesDisponibles"));
            }

            XElement? variablesContainer = resultElement.Element(BanguatSoapNamespaces.Service + "Variables");

            IReadOnlyList<SoapVariable> variables = SoapXmlParsing.ParseList(
                variablesContainer, BanguatSoapNamespaces.Service + "Variable", SoapVariable.FromElement);

            var currencies = variables
                .Select(variable => new CurrencyCatalogEntry(new CurrencyCode(variable.Moneda), variable.Descripcion))
                .ToList();

            return Result.Success(new Response(currencies));
        }
    }
}
