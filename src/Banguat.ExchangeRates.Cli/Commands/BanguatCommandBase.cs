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

    protected bool TryResolveCurrency(string value, OutputMode mode, out CurrencyCode currency)
    {
        if (int.TryParse(value, out int numeric))
        {
            currency = new CurrencyCode(numeric);
            return true;
        }

        if (aliasCatalog.TryResolve(value, out currency))
        {
            return true;
        }

        string? suggestion = SuggestNearestAlias(value);
        string message = suggestion is null
            ? $"Unknown currency '{value}'. Run 'currencies' to see all codes."
            : $"Unknown currency '{value}'. Did you mean: {suggestion}? Run 'currencies' to see all codes.";
        Fail(message, mode);
        currency = default;
        return false;
    }

    private string? SuggestNearestAlias(string value)
    {
        const int maxSuggestDistance = 2;

        string? best = null;
        int bestDistance = int.MaxValue;

        foreach (string candidate in aliasCatalog.AllAliases)
        {
            int distance = LevenshteinDistance(value.ToUpperInvariant(), candidate.ToUpperInvariant());
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return bestDistance <= maxSuggestDistance ? best : null;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        int[,] distances = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++)
        {
            distances[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            distances[0, j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        for (int j = 1; j <= b.Length; j++)
        {
            int cost = a[i - 1] == b[j - 1] ? 0 : 1;
            distances[i, j] = Math.Min(
                Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                distances[i - 1, j - 1] + cost);
        }

        return distances[a.Length, b.Length];
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