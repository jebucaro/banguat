using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Features;

public static class GetCurrentRate
{
    public sealed record Query(CurrencyCode Currency) : IQuery<Response>;

    /// <summary>
    /// For USD, the live service returns a single reference rate rather than a bid/ask spread —
    /// in that case <see cref="Buy"/> and <see cref="Sell"/> are both set to that same reference rate.
    /// For every other currency, <see cref="Buy"/> and <see cref="Sell"/> are the service's actual buy/sell quote.
    /// </summary>
    public sealed record RatePoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record Response(IReadOnlyList<RatePoint> Rates);

    internal sealed class Handler(IBanguatSoapTransport transport) : IQueryHandler<Query, Response>
    {
        private const string OperationName = "Variables";

        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var request = new XElement(
                BanguatSoapNamespaces.Service + OperationName,
                new XElement(BanguatSoapNamespaces.Service + "variable", query.Currency.Value));

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
                .Descendants(BanguatSoapNamespaces.Service + "VariablesResult")
                .FirstOrDefault();

            if (resultElement is null)
            {
                return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape("Variables"));
            }

            XElement? cambioDiaContainer = resultElement.Element(BanguatSoapNamespaces.Service + "CambioDia");
            IReadOnlyList<SoapVar> vars = SoapXmlParsing.ParseList(
                cambioDiaContainer, BanguatSoapNamespaces.Service + "Var", SoapVar.FromElement);

            if (vars.Count > 0)
            {
                var points = vars.Select(v => new RatePoint(v.Fecha, v.Compra, v.Venta)).ToList();
                return Result.Success(new Response(points));
            }

            XElement? cambioDolarContainer = resultElement.Element(BanguatSoapNamespaces.Service + "CambioDolar");
            IReadOnlyList<SoapVarDolar> varDolars = SoapXmlParsing.ParseList(
                cambioDolarContainer, BanguatSoapNamespaces.Service + "VarDolar", SoapVarDolar.FromElement);

            if (varDolars.Count > 0)
            {
                var points = varDolars.Select(v => new RatePoint(v.Fecha, v.Referencia, v.Referencia)).ToList();
                return Result.Success(new Response(points));
            }

            return Result.Success(new Response([]));
        }
    }
}
