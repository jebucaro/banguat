using System.Globalization;
using Banguat.ExchangeRates;
using Banguat.ExchangeRates.Common;
using Banguat.ExchangeRates.Features;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using Spectre.Console;

namespace Banguat.ExchangeRates.Cli.Commands;

internal sealed record RatePoint(DateOnly Date, decimal Buy, decimal Sell);

[Command("rate history",
    Description = "Show a currency's rate history, either from a date to today (--since) or over a bounded " +
                  "range (--from/--to), at most 366 days from 1989-11-06 onward. Defaults to USD (2) if " +
                  "--currency is omitted.")]
public sealed partial class RateHistoryCommand(
    IBanguatExchangeRateClient client,
    ICurrencyResolver currencyResolver,
    IAnsiConsole console,
    ICurrencyAliasCatalog aliasCatalog)
    : BanguatCommandBase(console, aliasCatalog), ICommand
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

        if (await ResolveCurrencyAsync(currencyResolver, Currency, mode) is not CurrencyCode currency)
        {
            return;
        }

        string? currencyAlias = GetAliasFor(currency);

        if (!TryParseOptionalDate("since", Since, mode, out DateOnly? since) ||
            !TryParseOptionalDate("from", From, mode, out DateOnly? from) ||
            !TryParseOptionalDate("to", To, mode, out DateOnly? to))
        {
            return;
        }

        if (!TryUnwrap(await client.GetRateHistoryAsync(currency, since, from, to), mode,
                out GetRateHistory.Response response))
        {
            return;
        }

        List<RatePoint> points = response.Rates.Select(r => new RatePoint(r.Date, r.Buy, r.Sell)).ToList();

        if (mode == OutputMode.Json)
        {
            await WriteJsonAsync(new
            {
                currency = currency.Value,
                currencyAlias,
                count = points.Count,
                history = points.Select(p => new { date = p.Date, buy = p.Buy, sell = p.Sell })
            });
            return;
        }

        if (points.Count == 0)
        {
            WriteNotice("No rate data found for the given range.", mode);
            return;
        }

        Table table = CreateTable(mode,
            ("Date", "date"), ("Currency", "currency"), ("Alias", "currencyAlias"), ("Buy", "buy"), ("Sell", "sell"));
        string currencyText = currency.Value.ToString(CultureInfo.InvariantCulture);
        string aliasText = currencyAlias ?? string.Empty;

        foreach (RatePoint point in points)
        {
            AddRow(table, mode,
                (point.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "grey"),
                (currencyText, "yellow"),
                (aliasText, "grey"),
                (point.Buy.ToString(CultureInfo.InvariantCulture), "green"),
                (point.Sell.ToString(CultureInfo.InvariantCulture), "green"));
        }

        Console.Write(table);
        WriteCount(points.Count, "rate points", mode);
    }

    private bool TryParseOptionalDate(string optionName, string? value, OutputMode mode, out DateOnly? date)
    {
        date = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!TryParseDate(optionName, value, mode, out DateOnly parsed))
        {
            return false;
        }

        date = parsed;
        return true;
    }
}