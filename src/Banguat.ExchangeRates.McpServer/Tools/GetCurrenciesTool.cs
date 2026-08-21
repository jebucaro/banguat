using System.ComponentModel;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Banguat.ExchangeRates.McpServer.Tools;

[McpServerToolType]
public sealed class GetCurrenciesTool(IBanguatExchangeRateClient client, ICurrencyAliasCatalog aliasCatalog)
{
    public sealed record CurrencyEntry(int Code, string Description, string? Alias);

    public sealed record CurrenciesResult(int Count, IReadOnlyList<CurrencyEntry> Currencies);

    [McpServerTool(Name = "get_currencies")]
    [Description(
        "Lists the currencies available from the Banguat exchange rate service, with their numeric codes and known aliases.")]
    public async Task<CurrenciesResult> GetCurrenciesAsync(CancellationToken cancellationToken = default)
    {
        Result<GetAvailableCurrencies.Response> result = await client.GetAvailableCurrenciesAsync(cancellationToken);
        if (result.IsFailure)
        {
            throw new McpException(result.Error.Description);
        }

        List<CurrencyEntry> currencies = result.Value.Currencies
            .Select(c => new CurrencyEntry(c.Code.Value, c.Description, aliasCatalog.GetAlias(c.Code)))
            .ToList();

        return new CurrenciesResult(currencies.Count, currencies);
    }
}