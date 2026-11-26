using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Features;

/// <summary>
/// Banguat has separate SOAP operations for open-ended and bounded history. This picks the right one so every host
/// shares one rule for valid inputs.
/// </summary>
public static class GetRateHistory
{
    public sealed record Query(
        CurrencyCode Currency,
        DateOnly? Since = null,
        DateOnly? From = null,
        DateOnly? To = null) : IQuery<Response>, ICacheableQuery
    {
        // Ranges that ended before today can't gain points. Ones reaching today, and the open-ended "since", get
        // refetched after the Guatemala day rolls over to pick up the new rate.
        public TimeSpan GetCacheDuration(IRateCalendar calendar)
        {
            return To is { } to ? calendar.LifetimeForRangeEnding(to) : calendar.TimeUntilNextDay;
        }
    }

    public sealed record RatePoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record Response(IReadOnlyList<RatePoint> Rates);

    internal sealed class Handler(IBanguatSoapTransport transport, IRateCalendar calendar)
        : IQueryHandler<Query, Response>
    {
        private const string SinceOperationName = "TipoCambioFechaInicialMoneda";
        private const string RangeOperationName = "TipoCambioRangoMoneda";

        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            Activity.Current?.SetTag("banguat.currency", query.Currency.Value);

            string operationName;
            XElement request;

            // Only "since" alone or both ends are valid. Anything else is rejected before any network call, so
            // hosts don't each copy the rule.
            if (query is { Since: { } since, From: null, To: null })
            {
                Result limits = RateHistoryLimits.Validate(since, calendar.Today, calendar);
                if (limits.IsFailure)
                {
                    return Result.Failure<Response>(limits.Error);
                }

                (operationName, request) = BuildSinceRequest(since, query.Currency);
            }
            else if (query is { Since: null, From: { } from, To: { } to })
            {
                Result limits = RateHistoryLimits.Validate(from, to, calendar);
                if (limits.IsFailure)
                {
                    return Result.Failure<Response>(limits.Error);
                }

                (operationName, request) = BuildRangeRequest(from, to, query.Currency);
            }
            else
            {
                return Result.Failure<Response>(BanguatErrors.InvalidHistoryRange());
            }

            Result<XDocument> transportResult = await transport.InvokeAsync(operationName, request, cancellationToken);

            return transportResult.IsFailure
                ? Result.Failure<Response>(transportResult.Error)
                : Parser.Parse(transportResult.Value, operationName);
        }

        private static (string, XElement) BuildSinceRequest(DateOnly since, CurrencyCode currency)
        {
            Activity.Current?.SetTag("banguat.date.since", since.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            return (SinceOperationName, new XElement(
                BanguatSoapNamespaces.Service + SinceOperationName,
                new XElement(
                    BanguatSoapNamespaces.Service + "fechainit",
                    since.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                new XElement(BanguatSoapNamespaces.Service + "moneda", currency.Value)));
        }

        private static (string, XElement) BuildRangeRequest(DateOnly from, DateOnly to, CurrencyCode currency)
        {
            Activity.Current?.SetTag("banguat.date.from", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Activity.Current?.SetTag("banguat.date.to", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

            return (RangeOperationName, new XElement(
                BanguatSoapNamespaces.Service + RangeOperationName,
                new XElement(
                    BanguatSoapNamespaces.Service + "fechainit",
                    from.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                new XElement(
                    BanguatSoapNamespaces.Service + "fechafin",
                    to.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)),
                new XElement(BanguatSoapNamespaces.Service + "moneda", currency.Value)));
        }
    }

    internal static class Parser
    {
        // Both operations return the same <Vars>/<Var> payload inside a "{operation}Result" element.
        internal static Result<Response> Parse(XDocument document, string operationName)
        {
            XElement? resultElement = document
                .Descendants(BanguatSoapNamespaces.Service + $"{operationName}Result")
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