using System.Globalization;
using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Api.Common;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Banguat.ExchangeRates.Api.Endpoints;

public sealed class GetRateHistoryEndpoint : IEndpoint
{
    public sealed record RateHistoryPoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record RateHistoryResponse(
        int Currency, string? CurrencyAlias, int Count, IReadOnlyList<RateHistoryPoint> History);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/rates/{currency}/history", HandleAsync)
            .WithTags("Rates")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway);
    }

    internal static async Task<Results<Ok<RateHistoryResponse>, ProblemHttpResult>> HandleAsync(
        string currency,
        string? since,
        string? from,
        string? to,
        IBanguatExchangeRateClient client,
        ICurrencyAliasCatalog aliasCatalog,
        CancellationToken cancellationToken)
    {
        if (!CurrencyRouteBinder.TryResolve(currency, aliasCatalog, out CurrencyCode code))
        {
            return CurrencyRouteBinder.UnknownCurrencyProblem(currency);
        }

        bool sinceGiven = !string.IsNullOrWhiteSpace(since);
        bool fromGiven = !string.IsNullOrWhiteSpace(from);
        bool toGiven = !string.IsNullOrWhiteSpace(to);
        bool rangeGiven = fromGiven && toGiven;
        bool anyRangePartGiven = fromGiven || toGiven;

        if (!((sinceGiven && !anyRangePartGiven) || (!sinceGiven && rangeGiven)))
        {
            return TypedResults.Problem(
                detail: "Provide either \"since\", or both \"from\" and \"to\" (not both, not neither).",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid date range parameters");
        }

        string? alias = aliasCatalog.GetAliases(code).FirstOrDefault();
        List<RateHistoryPoint> points;

        if (sinceGiven)
        {
            if (!TryParseDate(since!, out DateOnly sinceDate))
            {
                return InvalidDateProblem("since", since!);
            }

            Result<GetCurrencyRateHistorySince.Response> result =
                await client.GetCurrencyRateHistorySinceAsync(sinceDate, code, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblem();
            }

            points = result.Value.Rates.Select(r => new RateHistoryPoint(r.Date, r.Buy, r.Sell)).ToList();
        }
        else
        {
            if (!TryParseDate(from!, out DateOnly fromDate))
            {
                return InvalidDateProblem("from", from!);
            }

            if (!TryParseDate(to!, out DateOnly toDate))
            {
                return InvalidDateProblem("to", to!);
            }

            Result<GetCurrencyRateHistory.Response> result =
                await client.GetCurrencyRateHistoryAsync(fromDate, toDate, code, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.ToProblem();
            }

            points = result.Value.Rates.Select(r => new RateHistoryPoint(r.Date, r.Buy, r.Sell)).ToList();
        }

        return TypedResults.Ok(new RateHistoryResponse(code.Value, alias, points.Count, points));
    }

    private static bool TryParseDate(string value, out DateOnly date)
    {
        return DateOnly.TryParseExact(
            value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static ProblemHttpResult InvalidDateProblem(string paramName, string value)
    {
        return TypedResults.Problem(
            detail: $"Invalid value for \"{paramName}\": '{value}' is not a valid date. Expected format yyyy-MM-dd.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid date parameter");
    }
}
