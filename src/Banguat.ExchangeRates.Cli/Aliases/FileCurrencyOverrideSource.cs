using System.Text.Json;
using Banguat.ExchangeRates.Common;

namespace Banguat.ExchangeRates.Cli.Aliases;

public sealed class FileCurrencyOverrideSource(string? filePath = null) : ICurrencyOverrideSource
{
    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".banguat-cli", "currencies.json");

    private readonly string _filePath = filePath ?? DefaultPath;

    public IReadOnlyDictionary<string, CurrencyCode> Load()
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, CurrencyCode>(StringComparer.OrdinalIgnoreCase);
        }

        string json;
        try
        {
            json = File.ReadAllText(_filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CurrencyOverrideLoadException($"Failed to read {_filePath}: {ex.Message}");
        }

        Dictionary<string, int>? raw;
        try
        {
            raw = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
        }
        catch (JsonException ex)
        {
            throw new CurrencyOverrideLoadException(
                $"Failed to read {_filePath}: invalid JSON ({ex.Message}). Fix or remove the file to continue.");
        }

        Dictionary<string, CurrencyCode> result = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<CurrencyCode, string> seenAliasForCode = [];
        Dictionary<string, string> seenAliasKeys = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string alias, int value) in raw ?? [])
        {
            CurrencyCode code = new(value);

            if (seenAliasForCode.TryGetValue(code, out string? existingAlias))
            {
                throw new CurrencyOverrideLoadException(
                    $"Failed to read {_filePath}: currency code {code.Value} has more than one alias " +
                    $"('{existingAlias}' and '{alias}'). Each currency must have exactly one alias.");
            }

            if (seenAliasKeys.TryGetValue(alias, out string? existingAliasKey))
            {
                throw new CurrencyOverrideLoadException(
                    $"Failed to read {_filePath}: alias '{existingAliasKey}' and '{alias}' differ only by case. " +
                    "Aliases are case-insensitive, so each alias string may appear only once.");
            }

            seenAliasForCode[code] = alias;
            seenAliasKeys[alias] = alias;
            result[alias] = code;
        }

        return result;
    }
}