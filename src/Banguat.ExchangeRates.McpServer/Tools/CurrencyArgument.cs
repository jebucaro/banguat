using Banguat.ExchangeRates.Common;
using ModelContextProtocol;

namespace Banguat.ExchangeRates.McpServer.Tools;

public static class CurrencyArgument
{
    public const string Description =
        "Currency alias (e.g. \"usd\", \"eur\") or Banguat's own numeric code - NOT the ISO 4217 number " +
        "(USD is 2, not 840). See get_currencies. Defaults to \"usd\".";

    public static async Task<CurrencyCode> ResolveAsync(
        string currency, ICurrencyResolver currencyResolver, CancellationToken cancellationToken)
    {
        Result<CurrencyCode> resolved = await currencyResolver.ResolveAsync(currency, cancellationToken);
        if (resolved.IsFailure)
        {
            throw new McpException(resolved.Error.Type == ErrorType.NotFound
                ? $"{resolved.Error.Description} Call get_currencies to see valid codes and aliases."
                : resolved.Error.Description);
        }

        return resolved.Value;
    }
}