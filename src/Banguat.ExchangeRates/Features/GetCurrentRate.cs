using System.Diagnostics;
using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Features;

public static class GetCurrentRate
{
    public sealed record Query(CurrencyCode Currency) : IQuery<Response>, ICacheableQuery
    {
        // Banguat publishes at most one rate per Guatemala business day, so today's value stays valid
        // until the local day rolls over.
        public TimeSpan GetCacheDuration(IRateCalendar calendar)
        {
            return calendar.TimeUntilNextDay;
        }
    }

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
            Activity.Current?.SetTag("banguat.currency", query.Currency.Value);

            XElement request = new(
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
                return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape());
            }

            XElement? cambioDiaContainer = resultElement.Element(BanguatSoapNamespaces.Service + "CambioDia");
            IReadOnlyList<SoapVar>? vars = SoapXmlParsing.ParseList(
                cambioDiaContainer, BanguatSoapNamespaces.Service + "Var", SoapVar.FromElement);

            if (vars is null)
            {
                return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape());
            }

            if (vars.Count > 0)
            {
                if (!SoapXmlParsing.CheckTotalItems(resultElement, vars.Count))
                {
                    return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape());
                }

                List<RatePoint> points = vars.Select(v => new RatePoint(v.Fecha, v.Compra, v.Venta)).ToList();
                return Result.Success(new Response(points));
            }

            XElement? cambioDolarContainer = resultElement.Element(BanguatSoapNamespaces.Service + "CambioDolar");
            IReadOnlyList<SoapVarDolar>? varDolars = SoapXmlParsing.ParseList(
                cambioDolarContainer, BanguatSoapNamespaces.Service + "VarDolar", SoapVarDolar.FromElement);

            // TotalItems counts whichever list Banguat populated, or 0 when neither is.
            if (varDolars is null || !SoapXmlParsing.CheckTotalItems(resultElement, varDolars.Count))
            {
                return Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape());
            }

            if (varDolars.Count > 0)
            {
                List<RatePoint> points = varDolars.Select(v => new RatePoint(v.Fecha, v.Referencia, v.Referencia))
                    .ToList();
                return Result.Success(new Response(points));
            }

            return Result.Success(new Response([]));
        }
    }
}