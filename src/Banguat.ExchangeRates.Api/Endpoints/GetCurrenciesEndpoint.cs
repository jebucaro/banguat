using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Api.Common;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banguat.ExchangeRates.Api.Endpoints;

public sealed class GetCurrenciesEndpoint : IEndpoint
{
    public sealed record CurrencyEntry(int Code, string Description, string? Alias);

    public sealed record CurrenciesResponse(int Count, IReadOnlyList<CurrencyEntry> Currencies);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/currencies", HandleAsync)
            .WithTags("Currencies")
            .WithHttpCaching()
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    internal static async Task<Results<Ok<CurrenciesResponse>, ProblemHttpResult>> HandleAsync(
        IBanguatExchangeRateClient client, ICurrencyAliasCatalog aliasCatalog, CancellationToken cancellationToken)
    {
        Result<GetAvailableCurrencies.Response> result = await client.GetAvailableCurrenciesAsync(cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        // Ordered by code so the payload is canonical. The ETag is a hash of the serialized bytes, so if
        // the upstream SOAP service ever returned the catalog in a different order the tag would churn and
        // every revalidation would miss despite the data being unchanged.
        List<CurrencyEntry> currencies = result.Value.Currencies
            .OrderBy(c => c.Code.Value)
            .Select(c => new CurrencyEntry(c.Code.Value, c.Description, aliasCatalog.GetAlias(c.Code)))
            .ToList();

        return TypedResults.Ok(new CurrenciesResponse(currencies.Count, currencies));
    }
}