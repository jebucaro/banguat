using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Banguat.ExchangeRates.Common;
using CliFx.Binding;
using Spectre.Console;

namespace Banguat.ExchangeRates.Cli.Commands;

public enum OutputMode
{
    Plain,
    Rich,
    Json
}

public abstract class BanguatCommandBase(IAnsiConsole console)
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
            Console.WriteLine(JsonSerializer.Serialize(new { error = message }, JsonOptions));
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
