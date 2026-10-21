using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Diagnostics;
using Banguat.ExchangeRates.Soap;
using Banguat.ExchangeRates.Soap.Models;

namespace Banguat.ExchangeRates.Features;

public static class GetCurrentUsdRate
{
    public sealed record Query : IQuery<Response>, ICacheableQuery
    {
        // Banguat publishes at most one rate per Guatemala business day, so today's value stays valid
        // until the local day rolls over.
        public TimeSpan GetCacheDuration(IRateCalendar calendar)
        {
            return calendar.TimeUntilNextDay;
        }
    }

    public sealed record Response(DateOnly Date, decimal Rate);

    internal sealed class Handler(IBanguatSoapTransport transport) : IQueryHandler<Query, Response>
    {
        private const string OperationName = "TipoCambioDia";

        public async Task<Result<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            XElement request = new(BanguatSoapNamespaces.Service + OperationName);

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
            XElement? varDolarElement = document
                .Descendants(BanguatSoapNamespaces.Service + "TipoCambioDiaResult")
                .FirstOrDefault()?
                .Element(BanguatSoapNamespaces.Service + "CambioDolar")?
                .Element(BanguatSoapNamespaces.Service + "VarDolar");

            SoapVarDolar? varDolar = varDolarElement is null ? null : SoapVarDolar.FromElement(varDolarElement);

            return varDolar is null
                ? Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape())
                : Result.Success(new Response(varDolar.Fecha, varDolar.Referencia));
        }
    }
}