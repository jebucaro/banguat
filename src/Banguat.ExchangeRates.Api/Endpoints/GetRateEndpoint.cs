using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Api.Common;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banguat.ExchangeRates.Api.Endpoints;

public sealed class GetRateEndpoint : IEndpoint
{
    public sealed record RateResponse(
        int Currency,
        string? CurrencyAlias,
        DateOnly? Date,
        decimal? Buy,
        decimal? Sell);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/rates/{currency}", HandleAsync)
            .WithTags("Rates")
            .WithHttpCaching()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    internal static async Task<Results<Ok<RateResponse>, ProblemHttpResult>> HandleAsync(
        string currency,
        IBanguatExchangeRateClient client,
        ICurrencyResolver currencyResolver,
        ICurrencyAliasCatalog aliasCatalog,
        CancellationToken cancellationToken)
    {
        Result<CurrencyCode> resolved = await currencyResolver.ResolveAsync(currency, cancellationToken);
        if (resolved.IsFailure)
        {
            return resolved.Error.ToProblem();
        }

        CurrencyCode code = resolved.Value;

        string? alias = aliasCatalog.GetAlias(code);

        Result<GetCurrentRate.Response> result = await client.GetCurrentRateAsync(code, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        if (result.Value.Rates.Count == 0)
        {
            return TypedResults.Ok(new RateResponse(code.Value, alias, null, null, null));
        }

        GetCurrentRate.RatePoint point = result.Value.Rates[0];
        return TypedResults.Ok(new RateResponse(code.Value, alias, point.Date, point.Buy, point.Sell));
    }
}