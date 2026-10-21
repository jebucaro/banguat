using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Api.Common;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Banguat.ExchangeRates.Api.Endpoints;

public sealed class GetRateHistoryEndpoint : IEndpoint
{
    private const string SinceParameter = "since";
    private const string FromParameter = "from";
    private const string ToParameter = "to";

    /// <summary>The raw date query values, bound together so the handler stays under the parameter limit.</summary>
    internal sealed record HistoryRange(
        [FromQuery(Name = SinceParameter)] string? Since,
        [FromQuery(Name = FromParameter)] string? From,
        [FromQuery(Name = ToParameter)] string? To);

    public sealed record RateHistoryPoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record RateHistoryResponse(
        int Currency,
        string? CurrencyAlias,
        int Count,
        IReadOnlyList<RateHistoryPoint> History);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/rates/{currency}/history", HandleAsync)
            .WithTags("Rates")
            .WithHttpCaching(ResolveCacheLifetime)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout);
    }

    /// <summary>
    /// Picks the cache lifetime from the requested range: a range that already closed is stable, anything
    /// reaching today expires at the next Guatemala day so tomorrow's request sees today's rate.
    /// </summary>
    /// <remarks>
    /// Delegates to the same <see cref="RateCalendarExtensions.LifetimeForRangeEnding"/> rule the query
    /// record uses for its Valkey entry, so <c>max-age</c> and the server-side lifetime cannot disagree.
    /// </remarks>
    internal static TimeSpan ResolveCacheLifetime(HttpContext context, IRateCalendar calendar)
    {
        // The "since" form is open-ended, and a malformed "to" yields an uncached 400, so both fall through
        // to the daily boundary.
        return TryParseDate(context.Request.Query[ToParameter].ToString(), out DateOnly to)
            ? calendar.LifetimeForRangeEnding(to)
            : calendar.TimeUntilNextDay;
    }

    internal static async Task<Results<Ok<RateHistoryResponse>, ProblemHttpResult>> HandleAsync(
        string currency,
        [AsParameters] HistoryRange range,
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

        if (!TryParseOptionalDate(SinceParameter, range.Since, out DateOnly? sinceDate,
                out ProblemHttpResult? problem) ||
            !TryParseOptionalDate(FromParameter, range.From, out DateOnly? fromDate, out problem) ||
            !TryParseOptionalDate(ToParameter, range.To, out DateOnly? toDate, out problem))
        {
            return problem;
        }

        Result<GetRateHistory.Response> result =
            await client.GetRateHistoryAsync(code, sinceDate, fromDate, toDate, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        List<RateHistoryPoint> points =
            result.Value.Rates.Select(r => new RateHistoryPoint(r.Date, r.Buy, r.Sell)).ToList();

        return TypedResults.Ok(new RateHistoryResponse(code.Value, aliasCatalog.GetAlias(code), points.Count, points));
    }

    private static bool TryParseOptionalDate(
        string paramName,
        string? value,
        out DateOnly? date,
        [NotNullWhen(false)] out ProblemHttpResult? problem)
    {
        date = null;
        problem = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryParseDate(value, out DateOnly parsed))
        {
            problem = InvalidDateProblem(paramName, value);
            return false;
        }

        date = parsed;
        return true;
    }

    private static bool TryParseDate(string value, out DateOnly date)
    {
        return DateOnly.TryParseExact(
            value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static ProblemHttpResult InvalidDateProblem(string paramName, string value)
    {
        return TypedResults.Problem(
            $"Invalid value for \"{paramName}\": '{value}' is not a valid date. Expected format yyyy-MM-dd.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid date parameter");
    }
}