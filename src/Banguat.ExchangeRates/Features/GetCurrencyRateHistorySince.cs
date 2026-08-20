using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Features;

public static class GetCurrencyRateHistorySince
{
    public sealed record Query(DateOnly Since, CurrencyCode Currency) : IQuery<Response>;

    public sealed record RatePoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record Response(IReadOnlyList<RatePoint> Rates);

    internal sealed class Handler(IBanguatSoapTransport transport) : IQueryHandler<Query, Response>
    {
        private const string OperationName = "TipoCambioFechaInicialMoneda";

        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("banguat.currency", query.Currency.Value);
            Activity.Current?.SetTag("banguat.date.since", query.Since.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            var request = new XElement(
                BanguatSoapNamespaces.Service + OperationName,
                new XElement(
                    BanguatSoapNamespaces.Service + "fechainit",
                    query.Since.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                new XElement(BanguatSoapNamespaces.Service + "moneda", query.Currency.Value));

            var transportResult = await transport.InvokeAsync(OperationName, request, cancellationToken);

            return transportResult.IsFailure
                ? Result.Failure<Response>(transportResult.Error)
                : Parser.Parse(transportResult.Value);
        }
    }

    internal static class Parser
    {
        internal static Result<Response> Parse(XDocument document)
        {
            var resultElement = document
                .Descendants(BanguatSoapNamespaces.Service + "TipoCambioFechaInicialMonedaResult")
                .FirstOrDefault();

            if (resultElement is null)
                return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape("TipoCambioFechaInicialMoneda"));

            var varsContainer = resultElement.Element(BanguatSoapNamespaces.Service + "Vars");
            var vars = SoapXmlParsing.ParseList(
                varsContainer, BanguatSoapNamespaces.Service + "Var", SoapVar.FromElement);

            var points = vars.Select(v => new RatePoint(v.Fecha, v.Compra, v.Venta)).ToList();

            return Result.Success(new Response(points));
        }
    }
}