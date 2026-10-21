using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Features;

public static class GetUsdRateHistory
{
    public sealed record Query(DateOnly From, DateOnly To) : IQuery<Response>, ICacheableQuery
    {
        // A range that ended before today can no longer gain data points. A range that reaches today must
        // be refetched once the Guatemala day rolls over so tomorrow picks up today's published rate.
        public TimeSpan GetCacheDuration(IRateCalendar calendar)
        {
            return calendar.LifetimeForRangeEnding(To);
        }
    }

    public sealed record RatePoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record Response(IReadOnlyList<RatePoint> Rates);

    internal sealed class Handler(IBanguatSoapTransport transport, IRateCalendar calendar)
        : IQueryHandler<Query, Response>
    {
        private const string OperationName = "TipoCambioRango";

        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("banguat.date.from",
                query.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Activity.Current?.SetTag("banguat.date.to", query.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            Result limits = RateHistoryLimits.Validate(query.From, query.To, calendar);
            if (limits.IsFailure)
            {
                return Result.Failure<Response>(limits.Error);
            }

            XElement request = new(
                BanguatSoapNamespaces.Service + OperationName,
                new XElement(
                    BanguatSoapNamespaces.Service + "fechainit",
                    query.From.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                new XElement(
                    BanguatSoapNamespaces.Service + "fechafin",
                    query.To.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));

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
                .Descendants(BanguatSoapNamespaces.Service + "TipoCambioRangoResult")
                .FirstOrDefault();

            if (resultElement is null)
            {
                return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape());
            }

            XElement? varsContainer = resultElement.Element(BanguatSoapNamespaces.Service + "Vars");
            IReadOnlyList<SoapVar>? vars = SoapXmlParsing.ParseList(
                varsContainer, BanguatSoapNamespaces.Service + "Var", SoapVar.FromElement);

            if (vars is null || !SoapXmlParsing.CheckTotalItems(resultElement, vars.Count))
            {
                return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape());
            }

            List<RatePoint> points = vars.Select(v => new RatePoint(v.Fecha, v.Compra, v.Venta)).ToList();

            return Result.Success(new Response(points));
        }
    }
}