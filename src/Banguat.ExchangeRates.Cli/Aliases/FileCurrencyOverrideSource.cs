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
        foreach ((string alias, int value) in raw ?? [])
        {
            result[alias] = new CurrencyCode(value);
        }

        return result;
    }
}