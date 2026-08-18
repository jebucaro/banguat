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

internal sealed record RatePoint(DateOnly Date, decimal Buy, decimal Sell);

[Command("rate history",
    Description = "Show a currency's rate history, either from a date to today (--since) or over a bounded " +
                   "range (--from/--to). Defaults to USD (2) if --currency is omitted.")]
public sealed partial class RateHistoryCommand(
    IBanguatExchangeRateClient client,
    IAnsiConsole console,
    ICurrencyAliasCatalog aliasCatalog,
    ICurrencyOverrideSource overrideSource)
    : BanguatCommandBase(console, aliasCatalog, overrideSource), ICommand
{
    [CommandOption("since", Description = "Start date, format yyyy-MM-dd. Mutually exclusive with --from/--to.")]
    public string? Since { get; set; }

    [CommandOption("from", Description = "Start date, format yyyy-MM-dd. Requires --to.")]
    public string? From { get; set; }

    [CommandOption("to", Description = "End date, format yyyy-MM-dd. Requires --from.")]
    public string? To { get; set; }

    [CommandOption("currency", Description = "Currency code or alias (see 'currencies'). Defaults to 2 (USD).")]
    public string Currency { get; set; } = "2";

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (!TryParseOutputMode(out OutputMode mode))
        {
            return;
        }

        if (!TryValidateHistoryOptions(mode))
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

        IReadOnlyList<RatePoint> points;

        if (!string.IsNullOrWhiteSpace(Since))
        {
            if (!TryParseDate("since", Since, mode, out DateOnly since))
            {
                return;
            }

            if (!TryUnwrap(await client.GetCurrencyRateHistorySinceAsync(since, currency), mode, out var response))
            {
                return;
            }

            points = response.Rates.Select(r => new RatePoint(r.Date, r.Buy, r.Sell)).ToList();
        }
        else
        {
            if (!TryParseDate("from", From!, mode, out DateOnly from) ||
                !TryParseDate("to", To!, mode, out DateOnly to))
            {
                return;
            }

            if (!TryUnwrap(await client.GetCurrencyRateHistoryAsync(from, to, currency), mode, out var response))
            {
                return;
            }

            points = response.Rates.Select(r => new RatePoint(r.Date, r.Buy, r.Sell)).ToList();
        }

        if (mode == OutputMode.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                currency = currency.Value,
                currencyAlias,
                count = points.Count,
                history = points.Select(p => new { date = p.Date, buy = p.Buy, sell = p.Sell })
            }, JsonOptions));
            return;
        }

        if (points.Count == 0)
        {
            const string message = "No rate data found for the given range.";
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

        Table table = new Table().Border(mode == OutputMode.Rich ? TableBorder.Rounded : TableBorder.None);
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Date[/]" : "date"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Currency[/]" : "currency"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Alias[/]" : "currencyAlias"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Buy[/]" : "buy"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Sell[/]" : "sell"));

        string currencyText = currency.Value.ToString(CultureInfo.InvariantCulture);
        string aliasText = currencyAlias ?? string.Empty;

        foreach (var point in points)
        {
            string date = point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string buy = point.Buy.ToString(CultureInfo.InvariantCulture);
            string sell = point.Sell.ToString(CultureInfo.InvariantCulture);

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
                table.AddRow(
                    new Text(date), new Text(currencyText), new Text(aliasText), new Text(buy), new Text(sell));
            }
        }

        Console.Write(table);

        if (mode == OutputMode.Rich)
        {
            Console.MarkupLine($"[grey]Showing {points.Count} rate points[/]");
        }
        else
        {
            Console.WriteLine($"# count: {points.Count}");
        }
    }

    private bool TryValidateHistoryOptions(OutputMode mode)
    {
        bool sinceGiven = !string.IsNullOrWhiteSpace(Since);
        bool fromGiven = !string.IsNullOrWhiteSpace(From);
        bool toGiven = !string.IsNullOrWhiteSpace(To);
        bool rangeGiven = fromGiven && toGiven;
        bool anyRangePartGiven = fromGiven || toGiven;

        bool valid = (sinceGiven && !anyRangePartGiven) || (!sinceGiven && rangeGiven);

        if (!valid)
        {
            Fail("Provide either --since, or both --from and --to (not both, not neither).", mode);
            return false;
        }

        return true;
    }
}
