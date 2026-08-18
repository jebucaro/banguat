using System.Globalization;
using System.Text.Json;
using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Cli.Aliases;
using Banguat.ExchangeRates.Common;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using Spectre.Console;

namespace Banguat.ExchangeRates.Cli.Commands;

[Command("rate", Description = "Show today's buy/sell rate for a currency. Defaults to USD (2) if --currency is omitted.")]
public sealed partial class RateCommand(
    IBanguatExchangeRateClient client,
    IAnsiConsole console,
    ICurrencyAliasCatalog aliasCatalog,
    ICurrencyOverrideSource overrideSource)
    : BanguatCommandBase(console, aliasCatalog, overrideSource), ICommand
{
    [CommandOption("currency", Description = "Currency code or alias (see 'currencies'). Defaults to 2 (USD).")]
    public string Currency { get; set; } = "2";

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (!TryParseOutputMode(out OutputMode mode))
        {
            return;
        }

        if (!TryResolveCurrency(Currency, mode, out CurrencyCode currency))
        {
            return;
        }

        if (!TryLoadOverrideMap(mode, out var overrides))
        {
            return;
        }

        string? currencyAlias = GetAliasesFor(currency, overrides).FirstOrDefault();

        if (!TryUnwrap(await client.GetCurrentRateAsync(currency), mode, out var response))
        {
            return;
        }

        if (response.Rates.Count == 0)
        {
            if (mode == OutputMode.Json)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    new { currency = currency.Value, currencyAlias, count = 0 }, JsonOptions));
                return;
            }

            const string message = "No rate data available for today.";
            if (mode == OutputMode.Rich)
            {
                Console.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
            }
            else
            {
                Console.WriteLine(message);
            }

            return;
        }

        var point = response.Rates[0];
        string hint = $"rate history --since <date> --currency {currency.Value}";

        if (mode == OutputMode.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                date = point.Date,
                currency = currency.Value,
                currencyAlias,
                buy = point.Buy,
                sell = point.Sell,
                help = new[] { hint }
            }, JsonOptions));
            return;
        }

        Table table = new Table().Border(mode == OutputMode.Rich ? TableBorder.Rounded : TableBorder.None);
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Date[/]" : "date"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Currency[/]" : "currency"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Alias[/]" : "currencyAlias"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Buy[/]" : "buy"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Sell[/]" : "sell"));

        string date = point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string buy = point.Buy.ToString(CultureInfo.InvariantCulture);
        string sell = point.Sell.ToString(CultureInfo.InvariantCulture);
        string currencyText = currency.Value.ToString(CultureInfo.InvariantCulture);
        string aliasText = currencyAlias ?? string.Empty;

        if (mode == OutputMode.Rich)
        {
            table.AddRow(
                new Markup($"[grey]{date}[/]"),
                new Markup($"[yellow]{currencyText}[/]"),
                new Markup($"[grey]{Markup.Escape(aliasText)}[/]"),
                new Markup($"[green]{buy}[/]"),
                new Markup($"[green]{sell}[/]"));
        }
        else
        {
            table.AddRow(new Text(date), new Text(currencyText), new Text(aliasText), new Text(buy), new Text(sell));
        }

        Console.Write(table);

        if (mode == OutputMode.Rich)
        {
            Console.Write(new Panel(Markup.Escape(hint)).Header("Next steps"));
        }
        else
        {
            Console.WriteLine($"# hint: {hint}");
        }
    }
}
