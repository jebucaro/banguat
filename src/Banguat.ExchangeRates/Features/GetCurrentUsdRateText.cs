using System.Xml.Linq;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Common.Messaging;
using Banguat.ExchangeRates.Soap;

namespace Banguat.ExchangeRates.Features;

public static class GetCurrentUsdRateText
{
    public sealed record Query : IQuery<Response>;

    public sealed record Response(string Text);

    internal sealed class Handler(IBanguatSoapTransport transport) : IQueryHandler<Query, Response>
    {
        private const string OperationName = "TipoCambioDiaString";

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
            string? text = document
                .Descendants(BanguatSoapNamespaces.Service + "TipoCambioDiaStringResult")
                .FirstOrDefault()?
                .Value;

            return string.IsNullOrWhiteSpace(text)
                ? Result.Failure<Response>(BanguatErrors.UnexpectedResponseShape("TipoCambioDiaString"))
                : Result.Success(new Response(text));
        }
    }
}
