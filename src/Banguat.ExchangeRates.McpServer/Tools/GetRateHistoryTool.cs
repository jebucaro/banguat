using System.ComponentModel;
using System.Globalization;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Banguat.ExchangeRates.McpServer.Tools;

[McpServerToolType]
public sealed class GetRateHistoryTool(
    IBanguatExchangeRateClient client,
    ICurrencyResolver currencyResolver,
    ICurrencyAliasCatalog aliasCatalog)
{
    public sealed record RateHistoryPoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record RateHistoryResult(
        int Currency,
        string? CurrencyAlias,
        int Count,
        IReadOnlyList<RateHistoryPoint> History);

    [McpServerTool(Name = "get_rate_history")]
    [Description("Gets a currency's rate history, either from a date to today (since) or over a bounded " +
                 "range (from/to). Dates run from 1989-11-06 to today, and one call covers at most 366 days; " +
                 "split longer periods into several calls. Defaults to USD if currency is omitted.")]
    public async Task<RateHistoryResult> GetRateHistoryAsync(
        [Description(CurrencyArgument.Description)]
        string currency = "usd",
        [Description("Start date (yyyy-MM-dd), open-ended to today. Mutually exclusive with from/to.")]
        string? since = null,
        [Description("Start date (yyyy-MM-dd) of a bounded range. Requires to.")]
        string? from = null,
        [Description("End date (yyyy-MM-dd) of a bounded range. Requires from.")]
        string? to = null,
        CancellationToken cancellationToken = default)
    {
        CurrencyCode code = await CurrencyArgument.ResolveAsync(currency, currencyResolver, cancellationToken);
        Result<GetRateHistory.Response> result = await client.GetRateHistoryAsync(
            code, ParseOptionalDate(nameof(since), since), ParseOptionalDate(nameof(from), from),
            ParseOptionalDate(nameof(to), to),
            cancellationToken);
        if (result.IsFailure)
        {
            throw new McpException(result.Error.Description);
        }

        List<RateHistoryPoint> points =
            result.Value.Rates.Select(r => new RateHistoryPoint(r.Date, r.Buy, r.Sell)).ToList();

        return new RateHistoryResult(code.Value, aliasCatalog.GetAlias(code), points.Count, points);
    }

    private static DateOnly? ParseOptionalDate(string paramName, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : ParseDate(paramName, value);
    }

    private static DateOnly ParseDate(string paramName, string value)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None,
                out DateOnly date))
        {
            return date;
        }

        throw new McpException(
            $"Invalid value for \"{paramName}\": '{value}' is not a valid date. Expected format yyyy-MM-dd.");
    }
}