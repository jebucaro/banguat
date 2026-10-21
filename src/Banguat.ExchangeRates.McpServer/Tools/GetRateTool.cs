using System.ComponentModel;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Banguat.ExchangeRates.McpServer.Tools;

[McpServerToolType]
public sealed class GetRateTool(
    IBanguatExchangeRateClient client,
    ICurrencyResolver currencyResolver,
    ICurrencyAliasCatalog aliasCatalog)
{
    public sealed record RateResult(
        int Currency,
        string? CurrencyAlias,
        DateOnly? Date,
        decimal? Buy,
        decimal? Sell);

    [McpServerTool(Name = "get_rate")]
    [Description("Gets today's buy/sell exchange rate for a currency. Defaults to USD if currency is omitted.")]
    public async Task<RateResult> GetRateAsync(
        [Description(CurrencyArgument.Description)]
        string currency = "usd",
        CancellationToken cancellationToken = default)
    {
        CurrencyCode code = await CurrencyArgument.ResolveAsync(currency, currencyResolver, cancellationToken);
        string? alias = aliasCatalog.GetAlias(code);

        Result<GetCurrentRate.Response> result = await client.GetCurrentRateAsync(code, cancellationToken);
        if (result.IsFailure)
        {
            throw new McpException(result.Error.Description);
        }

        if (result.Value.Rates.Count == 0)
        {
            return new RateResult(code.Value, alias, null, null, null);
        }

        GetCurrentRate.RatePoint point = result.Value.Rates[0];
        return new RateResult(code.Value, alias, point.Date, point.Buy, point.Sell);
    }
}