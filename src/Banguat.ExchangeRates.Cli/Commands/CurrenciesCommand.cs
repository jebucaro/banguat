using System.Text.Json;
using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Cli.Aliases;
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
    ICurrencyAliasCatalog aliasCatalog,
    ICurrencyOverrideSource overrideSource)
    : BanguatCommandBase(console, aliasCatalog, overrideSource), ICommand
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

        if (!TryLoadOverrideMap(mode, out IReadOnlyDictionary<string, CurrencyCode> overrides))
        {
            return;
        }

        if (!TryUnwrap(await client.GetAvailableCurrenciesAsync(), mode, out GetAvailableCurrencies.Response response))
        {
            return;
        }

        if (mode == OutputMode.Json)
        {
            System.Console.Out.WriteLine(JsonSerializer.Serialize(new
            {
                count = response.Currencies.Count,
                currencies = response.Currencies.Select(c =>
                {
                    IReadOnlyList<string> aliases = GetAliasesFor(c.Code, overrides);
                    object? alias = aliases.Count switch
                    {
                        0 => null,
                        1 => aliases[0],
                        _ => aliases
                    };

                    return new { code = c.Code.Value, description = c.Description, alias };
                }).ToList(),
                help = Hints
            }, JsonOptions));
            return;
        }

        Table table = new Table().Border(mode == OutputMode.Rich ? TableBorder.Rounded : TableBorder.None);
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Code[/]" : "code"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Description[/]" : "description"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Alias[/]" : "alias"));

        foreach (GetAvailableCurrencies.CurrencyCatalogEntry entry in response.Currencies)
        {
            string aliasText = string.Join(", ", GetAliasesFor(entry.Code, overrides));

            if (mode == OutputMode.Rich)
            {
                table.AddRow(
                    new Markup($"[yellow]{entry.Code}[/]"),
                    new Text(entry.Description),
                    new Markup($"[grey]{Markup.Escape(aliasText)}[/]"));
            }
            else
            {
                table.AddRow(new Text(entry.Code.ToString()), new Text(entry.Description), new Text(aliasText));
            }
        }

        Console.Write(table);

        if (mode == OutputMode.Rich)
        {
            Console.MarkupLine($"[grey]Showing {response.Currencies.Count} currencies[/]");
        }
        else
        {
            Console.WriteLine($"# count: {response.Currencies.Count}");
        }

        WriteNextSteps(Hints, mode);
    }
}