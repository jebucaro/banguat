using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Features;

public static class GetUsdRateHistorySince
{
    public sealed record Query(DateOnly Since) : IQuery<Response>, ICacheableQuery
    {
        // An open-ended range implicitly runs to today, so it must be refetched once the Guatemala day
        // rolls over in order to include the newly published rate.
        public TimeSpan GetCacheDuration(IRateCalendar calendar)
        {
            return calendar.TimeUntilNextDay;
        }
    }

    public sealed record RatePoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record Response(IReadOnlyList<RatePoint> Rates);

    internal sealed class Handler(IBanguatSoapTransport transport, IRateCalendar calendar)
        : IQueryHandler<Query, Response>
    {
        private const string OperationName = "TipoCambioFechaInicial";

        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("banguat.date.since",
                query.Since.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            Result limits = RateHistoryLimits.Validate(query.Since, calendar.Today, calendar);
            if (limits.IsFailure)
            {
                return Result.Failure<Response>(limits.Error);
            }

            XElement request = new(
                BanguatSoapNamespaces.Service + OperationName,
                new XElement(
                    BanguatSoapNamespaces.Service + "fechainit",
                    query.Since.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));

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
                .Descendants(BanguatSoapNamespaces.Service + "TipoCambioFechaInicialResult")
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