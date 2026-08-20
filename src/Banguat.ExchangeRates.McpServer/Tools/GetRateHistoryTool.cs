using System.ComponentModel;
using System.Globalization;
using Banguat.ExchangeRates.Common;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Banguat.ExchangeRates.McpServer.Tools;

[McpServerToolType]
public sealed class GetRateHistoryTool(IBanguatExchangeRateClient client, ICurrencyAliasCatalog aliasCatalog)
{
    public sealed record RateHistoryPoint(DateOnly Date, decimal Buy, decimal Sell);

    public sealed record RateHistoryResult(
        int Currency, string? CurrencyAlias, int Count, IReadOnlyList<RateHistoryPoint> History);

    [McpServerTool(Name = "get_rate_history")]
    [Description("Gets a currency's rate history, either from a date to today (since) or over a bounded " +
                 "range (from/to). Defaults to USD if currency is omitted.")]
    public async Task<RateHistoryResult> GetRateHistoryAsync(
        [Description("Currency code or alias (see get_currencies). Defaults to \"usd\".")] string currency = "usd",
        [Description("Start date (yyyy-MM-dd), open-ended to today. Mutually exclusive with from/to.")] string? since = null,
        [Description("Start date (yyyy-MM-dd) of a bounded range. Requires to.")] string? from = null,
        [Description("End date (yyyy-MM-dd) of a bounded range. Requires from.")] string? to = null,
        CancellationToken cancellationToken = default)
    {
        CurrencyCode code = CurrencyArgument.Resolve(currency, aliasCatalog);
        string? alias = aliasCatalog.GetAliases(code).FirstOrDefault();

        bool sinceGiven = !string.IsNullOrWhiteSpace(since);
        bool fromGiven = !string.IsNullOrWhiteSpace(from);
        bool toGiven = !string.IsNullOrWhiteSpace(to);
        bool rangeGiven = fromGiven && toGiven;
        bool anyRangePartGiven = fromGiven || toGiven;

        if (!((sinceGiven && !anyRangePartGiven) || (!sinceGiven && rangeGiven)))
        {
            throw new McpException("Provide either \"since\", or both \"from\" and \"to\" (not both, not neither).");
        }

        List<RateHistoryPoint> points;

        if (sinceGiven)
        {
            DateOnly sinceDate = ParseDate("since", since!);
            var result = await client.GetCurrencyRateHistorySinceAsync(sinceDate, code, cancellationToken);
            if (result.IsFailure)
            {
                throw new McpException(result.Error.Description);
            }

            points = result.Value.Rates.Select(r => new RateHistoryPoint(r.Date, r.Buy, r.Sell)).ToList();
        }
        else
        {
            DateOnly fromDate = ParseDate("from", from!);
            DateOnly toDate = ParseDate("to", to!);
            var result = await client.GetCurrencyRateHistoryAsync(fromDate, toDate, code, cancellationToken);
            if (result.IsFailure)
            {
                throw new McpException(result.Error.Description);
            }

            points = result.Value.Rates.Select(r => new RateHistoryPoint(r.Date, r.Buy, r.Sell)).ToList();
        }

        return new RateHistoryResult(code.Value, alias, points.Count, points);
    }

    private static DateOnly ParseDate(string paramName, string value)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
        {
            return date;
        }

        throw new McpException($"Invalid value for \"{paramName}\": '{value}' is not a valid date. Expected format yyyy-MM-dd.");
    }
}
