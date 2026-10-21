using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using Spectre.Console;

namespace Banguat.ExchangeRates.Cli.Commands;

[Command("currencies", Description = "List the currencies available from the Banguat exchange rate service.")]
public sealed partial class CurrenciesCommand(
    IBanguatExchangeRateClient client,
    IAnsiConsole console,
    ICurrencyAliasCatalog aliasCatalog)
    : BanguatCommandBase(console, aliasCatalog), ICommand
{
    private static readonly string[] Hints =
    [
        "rate --currency <id|alias>",
        "rate history --since <date> --currency <id|alias>"
    ];

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (!TryParseOutputMode(out OutputMode mode))
        {
            return;
        }

        if (!TryUnwrap(await client.GetAvailableCurrenciesAsync(), mode, out GetAvailableCurrencies.Response response))
        {
            return;
        }

        if (mode == OutputMode.Json)
        {
            await WriteJsonAsync(new
            {
                count = response.Currencies.Count,
                currencies = response.Currencies.Select(c => new
                {
                    code = c.Code.Value, description = c.Description, alias = GetAliasFor(c.Code)
                }).ToList(),
                help = Hints
            });
            return;
        }

        Table table = CreateTable(mode, ("Code", "code"), ("Description", "description"), ("Alias", "alias"));
        foreach (GetAvailableCurrencies.CurrencyCatalogEntry entry in response.Currencies)
        {
            AddRow(table, mode,
                (entry.Code.ToString(), "yellow"),
                (entry.Description, null),
                (GetAliasFor(entry.Code) ?? string.Empty, "grey"));
        }

        Console.Write(table);
        WriteCount(response.Currencies.Count, "currencies", mode);

        WriteNextSteps(Hints, mode);
    }
}