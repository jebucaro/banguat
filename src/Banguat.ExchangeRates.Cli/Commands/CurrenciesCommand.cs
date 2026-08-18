using System.Text.Json;
using Banguat.ExchangeRates;
using CliFx;
using CliFx.Binding;
using CliFx.Infrastructure;
using Spectre.Console;

namespace Banguat.ExchangeRates.Cli.Commands;

[Command("currencies", Description = "List the currencies available from the Banguat exchange rate service.")]
public sealed partial class CurrenciesCommand(IBanguatExchangeRateClient client, IAnsiConsole console)
    : BanguatCommandBase(console), ICommand
{
    private static readonly string[] Hints =
    [
        "rate --currency <id>",
        "rate history --since <date> --currency <id>"
    ];

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (!TryParseOutputMode(out OutputMode mode))
        {
            return;
        }

        if (!TryUnwrap(await client.GetAvailableCurrenciesAsync(), mode, out var response))
        {
            return;
        }

        if (mode == OutputMode.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                count = response.Currencies.Count,
                currencies = response.Currencies.Select(c => new { code = c.Code.Value, description = c.Description }).ToList(),
                help = Hints
            }, JsonOptions));
            return;
        }

        Table table = new Table().Border(mode == OutputMode.Rich ? TableBorder.Rounded : TableBorder.None);
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Code[/]" : "code"));
        table.AddColumn(new TableColumn(mode == OutputMode.Rich ? "[bold]Description[/]" : "description"));

        foreach (var entry in response.Currencies)
        {
            if (mode == OutputMode.Rich)
            {
                table.AddRow(new Markup($"[yellow]{entry.Code}[/]"), new Text(entry.Description));
            }
            else
            {
                table.AddRow(new Text(entry.Code.ToString()), new Text(entry.Description));
            }
        }

        Console.Write(table);

        if (mode == OutputMode.Rich)
        {
            Console.MarkupLine($"[grey]Showing {response.Currencies.Count} currencies[/]");
            Console.Write(new Panel(string.Join('\n', Hints.Select(Markup.Escape))).Header("Next steps"));
        }
        else
        {
            Console.WriteLine($"# count: {response.Currencies.Count}");
            foreach (string hint in Hints)
            {
                Console.WriteLine($"# hint: {hint}");
            }
        }
    }
}
