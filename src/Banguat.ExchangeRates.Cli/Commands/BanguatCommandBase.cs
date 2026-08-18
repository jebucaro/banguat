using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Banguat.ExchangeRates.Cli.Aliases;
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
    IAnsiConsole console, ICurrencyAliasCatalog aliasCatalog, ICurrencyOverrideSource overrideSource)
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private IReadOnlyDictionary<string, CurrencyCode>? _overrideMapCache;

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

    protected bool TryLoadOverrideMap(OutputMode mode, out IReadOnlyDictionary<string, CurrencyCode> overrides)
    {
        if (_overrideMapCache is not null)
        {
            overrides = _overrideMapCache;
            return true;
        }

        try
        {
            _overrideMapCache = overrideSource.Load();
            overrides = _overrideMapCache;
            return true;
        }
        catch (CurrencyOverrideLoadException ex)
        {
            Fail(ex.Message, mode);
            overrides = new Dictionary<string, CurrencyCode>();
            return false;
        }
    }

    protected IReadOnlyList<string> GetAliasesFor(CurrencyCode code, IReadOnlyDictionary<string, CurrencyCode> overrides)
    {
        IEnumerable<string> overrideAliases = overrides.Where(kvp => kvp.Value == code).Select(kvp => kvp.Key);

        return overrideAliases.Concat(aliasCatalog.GetAliases(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    protected bool TryResolveCurrency(string value, OutputMode mode, out CurrencyCode currency)
    {
        if (int.TryParse(value, out int numeric))
        {
            currency = new CurrencyCode(numeric);
            return true;
        }

        if (!TryLoadOverrideMap(mode, out var overrides))
        {
            currency = default;
            return false;
        }

        if (overrides.TryGetValue(value, out currency))
        {
            return true;
        }

        if (aliasCatalog.TryResolve(value, out currency))
        {
            return true;
        }

        string? suggestion = SuggestNearestAlias(value, overrides);
        string message = suggestion is null
            ? $"Unknown currency '{value}'. Run 'currencies' to see all codes, or add an alias in " +
              "~/.banguat-cli/currencies.json."
            : $"Unknown currency '{value}'. Did you mean: {suggestion}? Run 'currencies' to see all codes, " +
              "or add an alias in ~/.banguat-cli/currencies.json.";
        Fail(message, mode);
        currency = default;
        return false;
    }

    private string? SuggestNearestAlias(string value, IReadOnlyDictionary<string, CurrencyCode> overrides)
    {
        const int maxSuggestDistance = 2;

        IEnumerable<string> candidates = overrides.Keys.Concat(aliasCatalog.AllAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        string? best = null;
        int bestDistance = int.MaxValue;

        foreach (string candidate in candidates)
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

        for (int i = 0; i <= a.Length; i++) distances[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) distances[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[a.Length, b.Length];
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
