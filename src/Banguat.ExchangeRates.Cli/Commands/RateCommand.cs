using System.Globalization;
using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using Spectre.Console;

namespace Banguat.ExchangeRates.Cli.Commands;

[Command("rate",
    Description = "Show today's buy/sell rate for a currency. Defaults to USD (2) if --currency is omitted.")]
public sealed partial class RateCommand(
    IBanguatExchangeRateClient client,
    ICurrencyResolver currencyResolver,
    IAnsiConsole console,
    ICurrencyAliasCatalog aliasCatalog)
    : BanguatCommandBase(console, aliasCatalog), ICommand
{
    [CommandOption("currency", Description = "Currency code or alias (see 'currencies'). Defaults to 2 (USD).")]
    public string Currency { get; set; } = "2";

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (!TryParseOutputMode(out OutputMode mode))
        {
            return;
        }

        if (await ResolveCurrencyAsync(currencyResolver, Currency, mode) is not CurrencyCode currency)
        {
            return;
        }

        string? currencyAlias = GetAliasFor(currency);

        if (!TryUnwrap(await client.GetCurrentRateAsync(currency), mode, out GetCurrentRate.Response response))
        {
            return;
        }

        if (response.Rates.Count == 0)
        {
            if (mode == OutputMode.Json)
            {
                await WriteJsonAsync(new { currency = currency.Value, currencyAlias });
                return;
            }

            WriteNotice("No rate data available for today.", mode);
            return;
        }

        GetCurrentRate.RatePoint point = response.Rates[0];
        string hint = $"rate history --since <date> --currency {currency.Value}";

        if (mode == OutputMode.Json)
        {
            await WriteJsonAsync(new
            {
                date = point.Date,
                currency = currency.Value,
                currencyAlias,
                buy = point.Buy,
                sell = point.Sell,
                help = new[] { hint }
            });
            return;
        }

        Table table = CreateTable(mode,
            ("Date", "date"), ("Currency", "currency"), ("Alias", "currencyAlias"), ("Buy", "buy"), ("Sell", "sell"));
        AddRow(table, mode,
            (point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "grey"),
            (currency.Value.ToString(CultureInfo.InvariantCulture), "yellow"),
            (currencyAlias ?? string.Empty, "grey"),
            (point.Buy.ToString(CultureInfo.InvariantCulture), "green"),
            (point.Sell.ToString(CultureInfo.InvariantCulture), "green"));

        Console.Write(table);

        WriteNextSteps([hint], mode);
    }
}