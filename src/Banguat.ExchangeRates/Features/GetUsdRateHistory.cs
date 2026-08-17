using System.Globalization;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Features;

public static class GetUsdRateHistory
{
    public sealed record Query(DateOnly From, DateOnly To) : IQuery<Response>;

    public sealed record RatePoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record Response(IReadOnlyList<RatePoint> Rates);

    internal sealed class Handler(IBanguatSoapTransport transport) : IQueryHandler<Query, Response>
    {
        private const string OperationName = "TipoCambioRango";

        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            if (query.To < query.From) return Result.Failure<Response>(BanguatErrors.InvalidDateRange());

            var request = new XElement(
                BanguatSoapNamespaces.Service + OperationName,
                new XElement(
                    BanguatSoapNamespaces.Service + "fechainit",
                    query.From.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                new XElement(
                    BanguatSoapNamespaces.Service + "fechafin",
                    query.To.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));

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
                .Descendants(BanguatSoapNamespaces.Service + "TipoCambioRangoResult")
                .FirstOrDefault();

            if (resultElement is null)
                return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape("TipoCambioRango"));

            var varsContainer = resultElement.Element(BanguatSoapNamespaces.Service + "Vars");
            var vars = SoapXmlParsing.ParseList(
                varsContainer, BanguatSoapNamespaces.Service + "Var", SoapVar.FromElement);

            var points = vars.Select(v => new RatePoint(v.Fecha, v.Compra, v.Venta)).ToList();

            return Result.Success(new Response(points));
        }
    }
}