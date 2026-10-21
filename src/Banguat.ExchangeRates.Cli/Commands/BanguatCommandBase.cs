using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Banguat.ExchangeRates.Common;
using CliFx.Binding;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Banguat.ExchangeRates.Cli.Commands;

public enum OutputMode
{
    Plain,
    Rich,
    Json
}

public abstract class BanguatCommandBase(
    IAnsiConsole console,
    ICurrencyAliasCatalog aliasCatalog)
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [CommandOption("output", 'o', Description = "Output format: plain, rich, or json.")]
    public string Output { get; set; } = "rich";

    protected IAnsiConsole Console { get; } = console;

    protected bool TryParseOutputMode(out OutputMode mode)
    {
        switch (Output.ToLowerInvariant())
        {
            case "plain":
                mode = OutputMode.Plain;
                return true;
            case "rich":
                mode = OutputMode.Rich;
                return true;
            case "json":
                mode = OutputMode.Json;
                return true;
            default:
                mode = OutputMode.Rich;
                Fail($"Invalid value for --output: '{Output}'. Expected one of: plain, rich, json.", mode);
                return false;
        }
    }

    protected bool TryParseDate(string optionName, string value, OutputMode mode, out DateOnly date)
    {
        if (DateOnly.TryParseExact(
                value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        Fail(
            $"Invalid value for --{optionName}: '{value}' is not a valid date. Expected format yyyy-MM-dd.",
            mode);
        return false;
    }

    protected string? GetAliasFor(CurrencyCode code)
    {
        return aliasCatalog.GetAlias(code);
    }

    protected async Task<CurrencyCode?> ResolveCurrencyAsync(
        ICurrencyResolver currencyResolver, string value, OutputMode mode)
    {
        Result<CurrencyCode> resolved = await currencyResolver.ResolveAsync(value);
        if (resolved.IsFailure)
        {
            Fail(resolved.Error.Type == ErrorType.NotFound
                ? $"{resolved.Error.Description} Run 'currencies' to see all codes."
                : resolved.Error.Description, mode);
            return null;
        }

        return resolved.Value;
    }

    protected void WriteNextSteps(IReadOnlyList<string> hints, OutputMode mode)
    {
        if (hints.Count == 0)
        {
            return;
        }

        Console.WriteLine();

        if (mode == OutputMode.Rich)
        {
            Console.MarkupLine("[bold]Next steps:[/]");
            foreach (string hint in hints)
            {
                Console.MarkupLine($"  → {Markup.Escape(hint)}");
            }
        }
        else
        {
            Console.WriteLine("Next steps:");
            foreach (string hint in hints)
            {
                Console.WriteLine($"  → {hint}");
            }
        }
    }

    protected static Table CreateTable(OutputMode mode, params (string Rich, string Plain)[] headers)
    {
        Table table = new Table().Border(mode == OutputMode.Rich ? TableBorder.Rounded : TableBorder.None);
        foreach ((string rich, string plain) in headers)
        {
            table.AddColumn(new TableColumn(mode == OutputMode.Rich ? $"[bold]{rich}[/]" : plain));
        }

        return table;
    }

    /// <summary>Adds a row, coloring each cell that has a style when the output is rich.</summary>
    protected static void AddRow(Table table, OutputMode mode, params (string Text, string? Style)[] cells)
    {
        table.AddRow(cells.Select(cell => mode == OutputMode.Rich && cell.Style is not null
            ? new Markup($"[{cell.Style}]{Markup.Escape(cell.Text)}[/]")
            : (IRenderable)new Text(cell.Text)));
    }

    protected void WriteNotice(string message, OutputMode mode)
    {
        if (mode == OutputMode.Rich)
        {
            Console.MarkupLine($"[yellow]{Markup.Escape(message)}[/]");
        }
        else
        {
            Console.WriteLine(message);
        }
    }

    protected void WriteCount(int count, string noun, OutputMode mode)
    {
        if (mode == OutputMode.Rich)
        {
            Console.MarkupLine($"[grey]Showing {count} {noun}[/]");
        }
        else
        {
            Console.WriteLine($"# count: {count}");
        }
    }

    // JSON goes straight to stdout so Spectre never wraps or styles it.
    protected static Task WriteJsonAsync(object payload)
    {
        return System.Console.Out.WriteLineAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    protected bool TryUnwrap<T>(Result<T> result, OutputMode mode, out T value)
    {
        if (result.IsFailure)
        {
            Fail(result.Error.Description, mode);
            value = default!;
            return false;
        }

        value = result.Value;
        return true;
    }

    protected void Fail(string message, OutputMode mode)
    {
        if (mode == OutputMode.Json)
        {
            System.Console.Out.WriteLine(JsonSerializer.Serialize(new { error = message }, JsonOptions));
        }
        else if (mode == OutputMode.Rich)
        {
            Console.MarkupLine($"[red]{Markup.Escape(message)}[/]");
        }
        else
        {
            Console.WriteLine(message);
        }

        Environment.ExitCode = 1;
    }
}