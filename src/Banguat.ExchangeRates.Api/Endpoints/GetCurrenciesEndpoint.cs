using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Api.Common;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banguat.ExchangeRates.Api.Endpoints;

public sealed class GetCurrenciesEndpoint : IEndpoint
{
    public sealed record CurrencyEntry(int Code, string Description, IReadOnlyList<string> Aliases);

    public sealed record CurrenciesResponse(int Count, IReadOnlyList<CurrencyEntry> Currencies);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/currencies", HandleAsync)
            .WithTags("Currencies")
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    internal static async Task<Results<Ok<CurrenciesResponse>, ProblemHttpResult>> HandleAsync(
        IBanguatExchangeRateClient client, ICurrencyAliasCatalog aliasCatalog, CancellationToken cancellationToken)
    {
        Result<GetAvailableCurrencies.Response> result = await client.GetAvailableCurrenciesAsync(cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        List<CurrencyEntry> currencies = result.Value.Currencies
            .Select(c => new CurrencyEntry(c.Code.Value, c.Description, aliasCatalog.GetAliases(c.Code)))
            .ToList();

        return TypedResults.Ok(new CurrenciesResponse(currencies.Count, currencies));
    }
}